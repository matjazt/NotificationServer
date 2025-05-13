using System.Net;
using System.Net.Sockets;
using System.Text;

namespace SmoothLib;

#region SvcWatchDogClient

/// <summary>
/// Provides a watchdog client for monitoring and managing timeouts of registered tasks.
/// The <see cref="SvcWatchDogClient"/> class allows tasks to be registered with a timeout and detects when they exceed their allowed
/// execution time. It external SvcWatchDog is present, it also periodically pings it via UDP and also detects its shutdown signaling.
/// IMPORTANT NOTE: SvcWatchDogClient is designed to detect if application or thread freezes. As soon as such condition is detected,
/// SvcWatchDogClient does everything it can to shut down the application: it stops sending UDP pings and the IsTimedOut starts
/// returning true, possibly ending the programs main loop.
/// It is therefore extremely important to set rather large timeout values, since you probably don't want to restart the service
/// for each minor performance hiccup.
/// </summary>
public class SvcWatchDogClient : IDisposable
{
    /// <summary>
    /// Gets or sets the main instance of the <see cref="SvcWatchDogClient"/>.
    /// </summary>
    public static SvcWatchDogClient Main { get; set; }

    private static string _section = "SvcWatchDogClient";

    // runtime
    private object _cs = new();
    private Task _backgroundLoopTask;
    private AutoResetEvent _trigger = new AutoResetEvent(false);
    private long _nextCheck = long.MaxValue;
    private bool _stopped;
    private string _udpPingTaskName = Guid.NewGuid().ToString();    // Unique name for the internal UDP ping task
    private Dictionary<string, long> _tasks = [];
    private HashSet<string> _timedOutTasks = [];

    // configuration
    private bool _ignoreTimeouts;
    private int _udpPingInterval;
    private string _shutdownEvent;
    private byte[] _watchdogSecret;
    private IPEndPoint _udpEndPoint;

    /// <summary>
    /// Initializes a new instance of the <see cref="SvcWatchDogClient"/> class.
    /// Loads configuration, sets up UDP pinging, and starts the background monitoring loop.
    /// </summary>
    public SvcWatchDogClient()
    {
        // If IgnoreTimeouts is set, then IsTimedOut() always returns false, regardless of the task timeouts.
        // This is useful for debugging, but shouldn't be used in production.
        _ignoreTimeouts = Config.Main.GetInt32(_section, "IgnoreTimeouts", 0) != 0;
        _udpPingInterval = Config.Main.GetInt32(_section, "UdpPingInterval", 10) * 1000;

        _watchdogSecret = Encoding.UTF8.GetBytes(Environment.GetEnvironmentVariable("WATCHDOG_SECRET") ?? "");
        _shutdownEvent = Environment.GetEnvironmentVariable("SHUTDOWN_EVENT");

        if (int.TryParse(Environment.GetEnvironmentVariable("WATCHDOG_PORT"), out int watchdogPort) && watchdogPort > 0 && _udpPingInterval > 0)
        {
            _udpEndPoint = new IPEndPoint(IPAddress.Parse("127.0.0.1"), watchdogPort);
            // Schedule the first immediate ping
            _tasks[_udpPingTaskName] = 1;
        }

        _backgroundLoopTask = Task.Run(BackgroundLoop);
        Lg.Information("started");
    }

    /// <summary>
    /// Stops the background monitoring loop and releases resources.
    /// </summary>
    public void Stop()
    {
        Lg.Information("stopping");
        _stopped = true;
        do
        {
            _trigger.Set();
        }
        while (_backgroundLoopTask.Wait(100) == false);
        Lg.Information("done");
    }

    /// <summary>
    /// Waits for a named shutdown event to be signaled, or times out after the specified interval.
    /// </summary>
    /// <param name="millisecondsTimeout">The maximum time to wait, in milliseconds.</param>
    /// <returns>True if the shutdown event was signaled; otherwise, false.</returns>
    public bool WaitForShutdownEvent(int millisecondsTimeout)
    {
        if (string.IsNullOrWhiteSpace(_shutdownEvent))
        {
            Thread.Sleep(millisecondsTimeout);
            return false;
        }

        // Create or open a global event
        using var globalEvent = new EventWaitHandle(
            false, // Initial state (not signaled)
            EventResetMode.ManualReset,
            _shutdownEvent
        );

        bool shutdownRequested = globalEvent.WaitOne(millisecondsTimeout);
        if (shutdownRequested)
        {
            Lg.Information("shutdown requested");
        }

        return shutdownRequested;
    }

    /// <summary>
    /// Determines whether any registered task has timed out.
    /// </summary>
    /// <returns>True if at least one task has timed out and timeouts are not ignored; otherwise, false.</returns>
    public bool IsTimedOut()
    {
        lock (_cs)
        {
            return _ignoreTimeouts == false && _timedOutTasks.Count > 0;
        }
    }

    /// <summary>
    /// Registers or updates a task with a timeout. If the task already exists, its timeout is refreshed.
    /// </summary>
    /// <param name="taskName">The unique name of the task.</param>
    /// <param name="timeoutSeconds">The timeout duration in seconds.</param>
    public void Ping(string taskName, int timeoutSeconds)
    {
        Lg.Verbose($"taskName={taskName}, timeoutSeconds={timeoutSeconds}");
        long taskCheckTime = Environment.TickCount64 + (timeoutSeconds * 1000);
        bool doTrigger;
        lock (_cs)
        {
            _tasks[taskName] = taskCheckTime;
            doTrigger = taskCheckTime < _nextCheck;
        }

        // If needed, trigger the background thread to recheck the tasks and recalculate the next check time
        if (doTrigger)
        {
            _trigger.Set();
        }
    }

    /// <summary>
    /// Removes a task from monitoring, closing its timeout.
    /// </summary>
    /// <param name="taskName">The name of the task to remove.</param>
    public void CloseTimeout(string taskName)
    {
        Lg.Verbose($"taskName={taskName}");
        lock (_cs)
        {
            _tasks.Remove(taskName);
        }
    }

    /// <summary>
    /// The main background loop that checks for task timeouts and sends UDP pings at configured intervals.
    /// </summary>
    private void BackgroundLoop()
    {
        Lg.Debug("running");
        while (!_stopped)
        {
            // Check all tasks
            long now = Environment.TickCount64;
            bool timeoutDetected = false;
            bool udpPingNeeded = false;
            lock (_cs)
            {
                _nextCheck = long.MaxValue;
                // create a copy of the task names to allow modifying the collection while iterating
                var taskNames = _tasks.Keys.ToList();
                foreach (string name in taskNames)
                {
                    long timeout = _tasks[name];
                    if (timeout <= now)
                    {
                        if (name == _udpPingTaskName)
                        {
                            // This is the internal ping task; we need to send a ping unless a timeout has been detected
                            if (!timeoutDetected)
                            {
                                timeout = _tasks[_udpPingTaskName] = now + _udpPingInterval;
                                udpPingNeeded = true;
                            }
                        }
                        else if (_timedOutTasks.Add(name))
                        {
                            // A new timed-out task has been detected
                            timeoutDetected = true;
                            _tasks.Remove(name);
                            // Prevent future UDP pings
                            _tasks.Remove(_udpPingTaskName);
                        }
                    }

                    if (timeout > now && timeout < _nextCheck)
                    {
                        // When the loop ends, _nextCheck contains the nearest future timeout. This way we can determine optimal wait time.
                        _nextCheck = timeout;
                    }
                }
            }

            // Perform logging and UDP ping outside the critical section
            if (timeoutDetected)
            {
                Lg.Error("timed out tasks: " + string.Join(",", _timedOutTasks));
            }
            else if (udpPingNeeded)
            {
                using var udpClient = new UdpClient();
                udpClient.Send(_watchdogSecret, _watchdogSecret.Length, _udpEndPoint);
                Lg.Verbose("UDP ping sent");
            }

            // Wait for the next timeout or a trigger, with a small buffer to avoid premature detection
            // 60 seconds max is just a safety measure.
            int waitTime = Math.Min(Math.Max((int)(_nextCheck - now + 50), 0), 60000);
            _trigger.WaitOne(waitTime);
        }

        Lg.Debug("done");
    }

    /// <summary>
    /// Releases resources used by the <see cref="SvcWatchDogClient"/> instance.
    /// </summary>
    /// <param name="disposing">True if called from <see cref="Dispose()"/>; otherwise, false.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            Stop();
            _trigger.Dispose();
        }
    }

    /// <summary>
    /// Disposes the <see cref="SvcWatchDogClient"/> and stops the background monitoring loop.
    /// </summary>
    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}

#endregion

//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

#region TimeoutDetector
/// <summary>
/// Represents a timeout detector that registers a task with the <see cref="SvcWatchDogClient"/> and automatically closes it when disposed.
/// This is useful for monitoring operations that should complete within a specified timeout period.
/// </summary>
public class TimeoutDetector : IDisposable
{
    /// <summary>
    /// Gets the name of the task within SvcWatchDogClient.
    /// </summary>
    public string Name { get; private set; }

    /// <summary>
    /// Gets information if the timeout has already been closed.
    /// </summary>
    public bool Closed { get; private set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="TimeoutDetector"/> class and registers it with the watchdog client.
    /// </summary>
    /// <param name="name">The base name of the task to monitor.</param>
    /// <param name="timeoutSeconds">The timeout in seconds for the task.</param>
    /// <param name="namePostfix">If true, appends a unique GUID to the task name to ensure uniqueness.</param>
    public TimeoutDetector(string name, int timeoutSeconds, bool namePostfix = true)
    {
        Name = namePostfix ? name + "_" + Guid.NewGuid().ToString() : name;
        SvcWatchDogClient.Main.Ping(Name, timeoutSeconds);
    }

    /// <summary>
    /// Closes the timeout (removes the task from SvcWatchDogClient).
    /// </summary>
    public void Close()
    {
        if (!Closed)
        {
            SvcWatchDogClient.Main.CloseTimeout(Name);
            Closed = true;
        }
    }

    /// <summary>
    /// Closes the timeout if not already closed.
    /// </summary>
    /// <param name="disposing">Indicates whether the method is called from Dispose.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            Close();
        }
    }

    /// <summary>
    /// Disposes the <see cref="TimeoutDetector"/> and closes the timeout if not already closed.
    /// </summary>
    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}

#endregion
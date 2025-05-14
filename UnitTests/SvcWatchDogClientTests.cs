using SmoothLib;

namespace UnitTests;

[Collection("Global Collection")]
public class SvcWatchDogClientTests
{

    private EventWaitHandle SimulateExternalWd()
    {
        // do what external SvcWatchDog would do
        string shutDownEventName = "shutDownEvent";
        Environment.SetEnvironmentVariable("SHUTDOWN_EVENT", shutDownEventName);
        Environment.SetEnvironmentVariable("WATCHDOG_SECRET", "rubbish");
        Environment.SetEnvironmentVariable("WATCHDOG_PORT", "12345");

        return new EventWaitHandle(
            false, // Initial state (not signaled)
            EventResetMode.ManualReset,
            shutDownEventName
        );
    }

    /// <summary>
    /// Test a normally enabled SvcWatchDogClient.
    /// </summary>
    [Fact]
    public void WatchDogTest1()
    {
        Lg.Information("starting");
        var shutDownEvent = SimulateExternalWd();

        var originalConfig = Config.Main;
        var testConfig = Config.FromJsonText("{}");
        Config.Main = testConfig;
        var wd = SvcWatchDogClient.Main = new SvcWatchDogClient();
        Config.Main = originalConfig; // Restore original config after SvcWatchDogClient initialization

        // Act & assert
        string task1 = "task1";
        string task2 = "task2";
        string task3 = "task3";

        Assert.False(wd.IsUdpPingingActive);
        Assert.False(wd.IsTimedOut);
        Assert.Empty(wd.TaskList);
        wd.Start();
        Assert.Single(wd.TaskList);
        Assert.True(wd.IsUdpPingingActive);
        Assert.False(wd.IsTimedOut);
        Assert.False(wd.WaitForShutdownEvent(1));
        wd.Ping(task1, 5);
        Thread.Sleep(1000);
        Assert.Equal(2, wd.TaskList.Count);
        Assert.False(wd.IsTimedOut);
        Assert.True(wd.IsUdpPingingActive);
        using (var lazy = new TimeoutDetector(task2, 2))
        {
            Thread.Sleep(1000);
        }

        Assert.Equal(2, wd.TaskList.Count);
        Assert.False(wd.IsTimedOut);
        Assert.True(wd.IsUdpPingingActive);

        wd.CloseTimeout(task1);
        Assert.Single(wd.TaskList);
        Assert.False(wd.IsTimedOut);
        Assert.True(wd.IsUdpPingingActive);

        using var lazy2 = new TimeoutDetector(task3, 1);
        Assert.Equal(2, wd.TaskList.Count);
        Thread.Sleep(1100);

        Assert.Empty(wd.TaskList);
        Assert.True(wd.IsTimedOut);
        Assert.False(wd.IsUdpPingingActive);

        Assert.False(wd.WaitForShutdownEvent(1));

        shutDownEvent.Set(); // Signal the shutdown event
        Assert.True(wd.WaitForShutdownEvent(1));

        // TODO: monitor UDP pings - actually receive the packets
        Lg.Information("done");
    }

    /// <summary>
    /// Test a disabled SvcWatchDogClient.
    /// </summary>
    [Fact]
    public void WatchDogTest2()
    {
        Lg.Information("starting");
        // Arrange
        SimulateExternalWd();

        var originalConfig = Config.Main;
        var testConfig = Config.FromJsonText("""
        {
            "SvcWatchDogClient": {
                "Enabled": false,
            }
        }
        """);
        Config.Main = testConfig;
        var wd = SvcWatchDogClient.Main = new SvcWatchDogClient();
        Config.Main = originalConfig; // Restore original config after SvcWatchDogClient initialization

        // Act & assert
        string task1 = "task1";
        string task2 = "task2";

        Assert.False(wd.IsUdpPingingActive);
        Assert.False(wd.IsTimedOut);
        Assert.Empty(wd.TaskList);
        wd.Start();
        Assert.Empty(wd.TaskList);
        Assert.False(wd.IsUdpPingingActive);
        Assert.False(wd.IsTimedOut);
        wd.Ping(task1, 1);
        Assert.Empty(wd.TaskList);
        Thread.Sleep(1200);
        Assert.Empty(wd.TaskList);
        Assert.False(wd.IsUdpPingingActive);
        Assert.False(wd.IsTimedOut);

        using (var lazy = new TimeoutDetector(task2, 3))
        {
            Thread.Sleep(1000);
        }

        Assert.Empty(wd.TaskList);
        Assert.False(wd.IsUdpPingingActive);
        Assert.False(wd.IsTimedOut);

        Lg.Information("done");
    }

    /// <summary>
    /// Test normally enabled SvcWatchDogClient, but without external SvcWatchDog.
    /// </summary>
    [Fact]
    public void WatchDogTest3()
    {
        Lg.Information("starting");
        // Arrange

        Environment.SetEnvironmentVariable("WATCHDOG_SECRET", null);
        Environment.SetEnvironmentVariable("WATCHDOG_PORT", null);

        var originalConfig = Config.Main;
        var testConfig = Config.FromJsonText("{}");
        Config.Main = testConfig;
        var wd = SvcWatchDogClient.Main = new SvcWatchDogClient();
        Config.Main = originalConfig; // Restore original config after SvcWatchDogClient initialization

        // Act & assert
        string task1 = "task1";
        string task2 = "task2";

        Assert.False(wd.IsUdpPingingActive);
        Assert.False(wd.IsTimedOut);
        Assert.Empty(wd.TaskList);
        wd.Start();
        Assert.Empty(wd.TaskList);
        Assert.False(wd.IsUdpPingingActive);
        Assert.False(wd.IsTimedOut);
        wd.Ping(task1, 1);
        Assert.Single(wd.TaskList);
        Thread.Sleep(1200);
        Assert.Empty(wd.TaskList);
        Assert.False(wd.IsUdpPingingActive);
        Assert.True(wd.IsTimedOut);

        using (var lazy = new TimeoutDetector(task2, 1))
        {
            Thread.Sleep(1200);
        }

        Assert.Empty(wd.TaskList);
        Assert.False(wd.IsUdpPingingActive);
        Assert.True(wd.IsTimedOut);

        Lg.Information("done");
    }
}

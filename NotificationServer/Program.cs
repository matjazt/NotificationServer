using Serilog;
using SmoothLib;

namespace NotificationServer;

sealed class MainClass
{
    private static string loggedInUsersThreadName = "loggedInUsersThread";
    private static EventWaitHandle loggedInUsersThreadEvent;
    static void Main(string[] args)
    {
        // BasicTools.DevelopmentMode = false;  // this should be automatically determined

        // this is a service, not a GUI application, so we need to set this flag in order to work within installation folder instead of AppData
        BasicTools.ServiceMode = true;

        // set default decimal and timestamp formats
        BasicTools.SetDefaultFormats();

        // set main password for the entire application - it is used to encrypt/decrypt sensitive configuration parameters and such
        BasicTools.SetDefaultPassword("yLCJt6ZcPVvILzwgQRKh");

        // figure out and CD to the correct working directory; create it if needed
        BasicTools.SetStartupFolder();

        // initialize configuration with default file name etc.
        Config.Main = Config.FromFile();

        // initialize logger
        LogTools.InitializeSerilog();

        // initialize SvcWatchDogClient
        SvcWatchDogClient.Main = new();
        string mainLoopTaskName = "mainLoop";
        // Register a timeout (Ping) first, then Start the watchdog's background thread.
        // Doing it the other way around could result in a (theoretical) freeze between Start and Ping going unnoticed.
        SvcWatchDogClient.Main.Ping(mainLoopTaskName, 30);
        SvcWatchDogClient.Main.Start();

        Notifications.Service.NotificationService.Main = new();

        // create rest service and run it in background
        var restService = new RestService();
        var webServiceTask = restService.App.RunAsync();

        // for demonstation purposes, we will also run a thread that lists logged in users
        // Create an event, which we'll use to shut down the thread when needed
        loggedInUsersThreadEvent = new EventWaitHandle(false, EventResetMode.ManualReset);
        // let's start monitoring it before it's even started - that's the only reliable way
        SvcWatchDogClient.Main.Ping(loggedInUsersThreadName, 30);
        var loggedInUsersThread = Task.Run(LoggedInUsersThread);

        while (webServiceTask.IsCompleted == false
            && SvcWatchDogClient.Main.WaitForShutdownEvent(500) == false
            && SvcWatchDogClient.Main.IsTimedOut == false)
        {
            SvcWatchDogClient.Main.Ping(mainLoopTaskName, 30);

            if (Console.KeyAvailable)
            {
                var keyInfo = Console.ReadKey(true);
                if (keyInfo.KeyChar == 27)
                {
                    break;
                }

                Lg.Information("key pressed: " + keyInfo.KeyChar);

                if (keyInfo.KeyChar == 'l')
                {
                    var users = WindowsSessions.GetLoggedInUsers(null);
                    foreach (var (sessionId, username) in users)
                    {
                        Lg.Information($"sessionId: {sessionId}, userName: {username}");
                    }
                }
            }
        }

        restService.Stop(webServiceTask);

        loggedInUsersThreadEvent.Set();
        loggedInUsersThread.Wait(1000);
        loggedInUsersThreadEvent.Dispose();

        Lg.Information("exiting");
        SvcWatchDogClient.Main.Stop();
        Log.CloseAndFlush();
    }

    private static void LoggedInUsersThread()
    {
        Lg.Debug("started");
        string userList = "";
        do
        {
            SvcWatchDogClient.Main.Ping(loggedInUsersThreadName, 60);

            var users = WindowsSessions.GetLoggedInUsers(null).OrderBy(x => x.sessionId).ThenBy(x => x.username);
            string newUserList = string.Join("\n", users.Select(x => $"{x.sessionId}: {x.username}"));

            if (newUserList != userList)
            {
                userList = newUserList;
                Lg.Information("currently logged in users:\n" + userList);
            }
        } while (loggedInUsersThreadEvent.WaitOne(30000) == false);
        Lg.Debug("stopped");
    }
}

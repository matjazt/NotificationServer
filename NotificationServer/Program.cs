using Serilog;
using SmoothLib;

namespace NotificationServer;

sealed class MainClass
{
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
            }
        }

        restService.Stop(webServiceTask);

        Lg.Information("exiting");
        SvcWatchDogClient.Main.Stop();
        Log.CloseAndFlush();
    }
}

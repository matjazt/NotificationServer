using Serilog;
using SmoothLib;

namespace UnitTests;

public class GlobalFixture : IAsyncLifetime

{
    public GlobalFixture()
    {
    }
    ValueTask IAsyncLifetime.InitializeAsync()
    {
        // this is a service, not a GUI application, so we need to set this flag in order to work within installation folder instead of AppData
        BasicTools.ServiceMode = true;

        // set default decimal and timestamp formats
        BasicTools.SetDefaultFormats();

        // set main password for the entire application - it is used to encrypt/decrypt sensitive configuration parameters and such
        BasicTools.SetDefaultPassword(null, "mTfTOZTq15Fwenjjk4Ek");

        // figure out and CD to the correct working directory; create it if needed
        BasicTools.SetStartupFolder();

        // initialize configuration with default file name etc.
        Config.Main = Config.FromFile();

        // initialize logger
        LogTools.InitializeSerilog();

        return ValueTask.CompletedTask;
    }

    ValueTask IAsyncDisposable.DisposeAsync()
    {
        Lg.Information("exiting");
        Log.CloseAndFlush();

        return ValueTask.CompletedTask;
    }
}

[CollectionDefinition("Global Collection")]
public class GlobalCollection : ICollectionFixture<GlobalFixture> { }


using Serilog;
using Serilog.Core;
using Serilog.Events;
using System.Runtime.CompilerServices;

namespace SmoothLib;

/// <summary>
/// Provides tools for initializing and configuring Serilog logging for the application.
/// Handles log file and console output configuration, log level settings, and output formatting.
/// Provides a wrapper for logging methods with automatic source file and method name logging.
/// </summary>
public static class LogTools
{
    private static string section = "LogTools";

    public static void InitializeSerilog()
    {
        string logFile = Config.Main.GetString(section, "LogFile", Path.Combine(BasicTools.AppDataFolder, "Log", BasicTools.AssemblyName + ".log"));
        string outputTemplate = Config.Main.GetString(section, "OutputTemplate", "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}]: {FormattedSourceContext}{Message:lj}{NewLine}{Exception}");

        Directory.CreateDirectory(Path.GetDirectoryName(logFile));

        var minimumFileLogLevel = Config.Main.GetEnum(section, "MinimumFileLogLevel", LogEventLevel.Verbose);
        var minimumConsoleLogLevel = Config.Main.GetEnum(section, "MinimumConsoleLogLevel", LogEventLevel.Verbose);

        // initialize serilog
        var loggerConfiguration = new LoggerConfiguration()
            .Enrich.FromLogContext()
            .Enrich.With(new ConditionalSourceContextEnricher()) // Add the custom enricher
            .MinimumLevel.Is(minimumConsoleLogLevel < minimumFileLogLevel ? minimumConsoleLogLevel : minimumFileLogLevel)
            .WriteTo.File(logFile,
                rollingInterval: Config.Main.GetEnum(section, "RollingInterval", RollingInterval.Day),
                restrictedToMinimumLevel: minimumFileLogLevel,
                retainedFileCountLimit: Config.Main.GetInt32(section, "RetainedFileCountLimit", 365),
                outputTemplate: outputTemplate,
                buffered: Config.Main.GetBool(section, "Buffered", true),
                flushToDiskInterval: new TimeSpan(0, 0, Config.Main.GetInt32(section, "FlushToDiskIntervalSeconds", 5)));    // TODO: preveri če tole slučajno za brez veze žre CPU

        if (Config.Main.GetBool(section, "ConsoleOutputEnabled", true))
        {
            loggerConfiguration.WriteTo.Console(
                outputTemplate: outputTemplate,
                restrictedToMinimumLevel: minimumConsoleLogLevel
                );
        }

        Log.Logger = loggerConfiguration.CreateLogger();
    }
}

public class ConditionalSourceContextEnricher : ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        if (logEvent.Properties.TryGetValue("SourceContext", out var sourceContext))
        {
            string sourceContextString = sourceContext.ToString().Trim('"'); // Remove quotes
            if (!string.IsNullOrEmpty(sourceContextString))
            {
                string formattedSourceContext = $"{sourceContextString}: ";
                logEvent.AddOrUpdateProperty(propertyFactory.CreateProperty("FormattedSourceContext", formattedSourceContext));
            }
        }
    }
}

/// <summary>
/// Log wrappers, supporting automatic source file and method name logging (but NOT structured logging).
/// </summary>
public static class Lg
{
    [MessageTemplateFormatMethod("messageTemplate")]
    public static void Verbose(string messageTemplate, [CallerMemberName] string memberName = "", [CallerFilePath] string sourceFilePath = "")
    {
        Log.Verbose(BasicTools.GetFileNameWithoutExtension(sourceFilePath) + "." + memberName + ": " + messageTemplate);
    }

    [MessageTemplateFormatMethod("messageTemplate")]
    public static void Verbose(Exception exception, string messageTemplate, [CallerMemberName] string memberName = "", [CallerFilePath] string sourceFilePath = "")
    {
        Log.Verbose(exception, BasicTools.GetFileNameWithoutExtension(sourceFilePath) + "." + memberName + ": " + messageTemplate);
    }

    [MessageTemplateFormatMethod("messageTemplate")]
    public static void Debug(string messageTemplate, [CallerMemberName] string memberName = "", [CallerFilePath] string sourceFilePath = "")
    {
        Log.Debug(BasicTools.GetFileNameWithoutExtension(sourceFilePath) + "." + memberName + ": " + messageTemplate);
    }

    [MessageTemplateFormatMethod("messageTemplate")]
    public static void Debug(Exception exception, string messageTemplate, [CallerMemberName] string memberName = "", [CallerFilePath] string sourceFilePath = "")
    {
        Log.Debug(exception, BasicTools.GetFileNameWithoutExtension(sourceFilePath) + "." + memberName + ": " + messageTemplate);
    }

    [MessageTemplateFormatMethod("messageTemplate")]
    public static void Information(string messageTemplate, [CallerMemberName] string memberName = "", [CallerFilePath] string sourceFilePath = "")
    {
        Log.Information(BasicTools.GetFileNameWithoutExtension(sourceFilePath) + "." + memberName + ": " + messageTemplate);
    }

    [MessageTemplateFormatMethod("messageTemplate")]
    public static void Information(Exception exception, string messageTemplate, [CallerMemberName] string memberName = "", [CallerFilePath] string sourceFilePath = "")
    {
        Log.Information(exception, BasicTools.GetFileNameWithoutExtension(sourceFilePath) + "." + memberName + ": " + messageTemplate);
    }

    [MessageTemplateFormatMethod("messageTemplate")]
    public static void Warning(string messageTemplate, [CallerMemberName] string memberName = "", [CallerFilePath] string sourceFilePath = "")
    {
        Log.Warning(BasicTools.GetFileNameWithoutExtension(sourceFilePath) + "." + memberName + ": " + messageTemplate);
    }

    [MessageTemplateFormatMethod("messageTemplate")]
    public static void Warning(Exception exception, string messageTemplate, [CallerMemberName] string memberName = "", [CallerFilePath] string sourceFilePath = "")
    {
        Log.Warning(exception, BasicTools.GetFileNameWithoutExtension(sourceFilePath) + "." + memberName + ": " + messageTemplate);
    }

    [MessageTemplateFormatMethod("messageTemplate")]
    public static void Error(string messageTemplate, [CallerMemberName] string memberName = "", [CallerFilePath] string sourceFilePath = "")
    {
        Log.Error(BasicTools.GetFileNameWithoutExtension(sourceFilePath) + "." + memberName + ": " + messageTemplate);
    }

    [MessageTemplateFormatMethod("messageTemplate")]
    public static void Error(Exception exception, string messageTemplate, [CallerMemberName] string memberName = "", [CallerFilePath] string sourceFilePath = "")
    {
        Log.Error(exception, BasicTools.GetFileNameWithoutExtension(sourceFilePath) + "." + memberName + ": " + messageTemplate);
    }

    [MessageTemplateFormatMethod("messageTemplate")]
    public static void Fatal(string messageTemplate, [CallerMemberName] string memberName = "", [CallerFilePath] string sourceFilePath = "")
    {
        Log.Fatal(BasicTools.GetFileNameWithoutExtension(sourceFilePath) + "." + memberName + ": " + messageTemplate);
    }

    [MessageTemplateFormatMethod("messageTemplate")]
    public static void Fatal(Exception exception, string messageTemplate, [CallerMemberName] string memberName = "", [CallerFilePath] string sourceFilePath = "")
    {
        Log.Fatal(exception, BasicTools.GetFileNameWithoutExtension(sourceFilePath) + "." + memberName + ": " + messageTemplate);
    }

    public static void Assert(bool condition, [CallerMemberName] string memberName = "", [CallerFilePath] string sourceFilePath = "", [CallerLineNumber] int lineNumber = 0)
    {
        if (!condition)
        {
            Log.Fatal(BasicTools.GetFileNameWithoutExtension(sourceFilePath) + "." + memberName + $": assertion failure at line #{lineNumber}");
        }
    }
}

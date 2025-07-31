using System.Text;

namespace SmoothLib;

/// <summary>
/// Provides configuration management for applications, supporting loading and retrieving configuration 
/// values from JSON files. The <c>Config</c> class allows you to load configuration from a file or an <see cref="IConfiguration"/>
/// instance and retrieve configuration values in various types (string, int, double, enum, etc.). 
/// It also supports encrypted values and automatic logging of accessed keys.
/// Note that the getter methods tolerates quoted numeric and boolean values, which is not standard, but might be useful.
/// </summary>
public class Config
{
    private object _cs = new object();
    private string _fileName;
    private Dictionary<string, Dictionary<string, string>> _autoLogData = [];

    public IConfiguration Configuration { get; }
    public static Config Main { get; set; }

    public Config(IConfiguration configuration)
    {
        Configuration = configuration;
    }

    public static Config FromFile(string fileName = null, bool optional = false, bool reloadOnChange = true)
    {
        return new Config(fileName, optional, reloadOnChange);
    }

    public static Config FromJsonText(string jsonText)
    {
        return new Config(jsonText);
    }

    private Config(string fileName, bool optional, bool reloadOnChange)
    {
        _fileName = fileName ?? Path.Combine(BasicTools.AppDataFolder, "etc", BasicTools.AssemblyName + ".json");

        string folderName = Path.GetDirectoryName(_fileName);
        Directory.CreateDirectory(folderName);
        var builder = new ConfigurationBuilder();
        builder.SetBasePath(folderName).AddJsonFile(Path.GetFileName(_fileName), optional: optional, reloadOnChange: reloadOnChange);
        Configuration = builder.Build();
    }

    private Config(string jsonText)
    {
        var builder = new ConfigurationBuilder();
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(jsonText));
        builder.AddJsonStream(stream);
        Configuration = builder.Build();
    }

    public string GetString(string section, string key, string defaultValue = null, bool autoLog = true)
    {
        string ret;
        bool doLog = false;
        lock (_cs)
        {
            ret = string.IsNullOrEmpty(section) ? Configuration[key] : Configuration.GetSection(section)[key];

            if (autoLog && ret != null)
            {
                // log only if the same value hasn't been logged before
                _autoLogData.TryGetValue(section, out var sectionData);
                if (sectionData == null)
                {
                    _autoLogData[section] = new() { { key, ret } };
                    doLog = true;
                }
                else if (!sectionData.TryGetValue(key, out string loggedValue) || loggedValue != ret)
                {
                    sectionData[key] = ret;
                    doLog = true;
                }
            }
        }

        if (doLog)
        {
            Lg.Information($"[{section}] {key}={ret}");
        }

        return ret ?? defaultValue;
    }

    public string GetEncryptedString(string section, string key, string defaultValue = null, string password = null, bool autoLog = false, bool autoLogEncryptionWarning = true, bool autoLogSuggestion = true, int suggestionDelay = 0)
    {
        string v = GetString(section, key, null, autoLog);
        if (v == null)
        {
            return defaultValue;
        }

        try
        {
            return BasicTools.Aes256CbcDecrypt(v, password);
        }
        catch
        {
            if (autoLogEncryptionWarning || autoLogSuggestion)
            {
                Task.Run(async () =>
                {
                    if (suggestionDelay > 0)
                    {
                        await Task.Delay(suggestionDelay); // wait for the specified delay before logging
                    }

                    if (autoLogEncryptionWarning)
                    {
                        Lg.Warning($"failed to decrypt [{section}] {key}, using it in plain form");
                    }

                    if (autoLogSuggestion)
                    {
                        Lg.Information($"you should use encrypted value for the [{section}] {key} parameter: {BasicTools.Aes256CbcEncrypt(v, password)}");
                    }
                });
            }

            return v;
        }
    }

    public int GetInt32(string section, string key, int defaultValue, bool autoLog = true)
    {
        string v = GetString(section, key, null, autoLog);

        if (String.IsNullOrWhiteSpace(v))
        {
            return defaultValue;
        }

        try
        {
            return v.StartsWith("0x", StringComparison.CurrentCultureIgnoreCase)
                ? Convert.ToInt32(v.Substring(2), 16)
                : int.Parse(v, BasicTools.NFI_DOT);
        }
        catch (Exception e) when (e is FormatException || e is OverflowException)
        {
            return defaultValue;
        }
    }

    public uint GetUInt32(string section, string key, uint defaultValue, bool autoLog = true)
    {
        string v = GetString(section, key, null, autoLog);

        if (String.IsNullOrWhiteSpace(v))
        {
            return defaultValue;
        }

        try
        {
            return v.StartsWith("0x", StringComparison.CurrentCultureIgnoreCase)
                ? Convert.ToUInt32(v.Substring(2), 16)
                : uint.Parse(v, BasicTools.NFI_DOT);
        }
        catch (Exception e) when (e is FormatException || e is OverflowException)
        {
            return defaultValue;
        }
    }

    public long GetInt64(string section, string key, long defaultValue, bool autoLog = true)
    {
        string v = GetString(section, key, null, autoLog);

        if (String.IsNullOrWhiteSpace(v))
        {
            return defaultValue;
        }

        try
        {
            return v.StartsWith("0x", StringComparison.CurrentCultureIgnoreCase)
                ? Convert.ToInt64(v.Substring(2), 16)
                : long.Parse(v, BasicTools.NFI_DOT);
        }
        catch (Exception e) when (e is FormatException || e is OverflowException)
        {
            return defaultValue;
        }
    }

    public ulong GetUInt64(string section, string key, ulong defaultValue, bool autoLog = true)
    {
        string v = GetString(section, key, null, autoLog);

        if (String.IsNullOrWhiteSpace(v))
        {
            return defaultValue;
        }

        try
        {
            return v.StartsWith("0x", StringComparison.CurrentCultureIgnoreCase)
                ? Convert.ToUInt64(v.Substring(2), 16)
                : ulong.Parse(v, BasicTools.NFI_DOT);
        }
        catch (Exception e) when (e is FormatException || e is OverflowException)
        {
            return defaultValue;
        }
    }

    public bool GetBool(string section, string key, bool defaultValue, bool autoLog = true)
    {
        string v = GetString(section, key, null, autoLog);

        if (String.IsNullOrWhiteSpace(v))
        {
            return defaultValue;
        }

        v = v.ToLower();

        if (long.TryParse(v, out long l))
        {
            return l != 0;
        }

        switch (v)
        {
            case "false":
                return false;
            case "true":
                return true;
            default:
                return defaultValue;
        }
    }

    public double GetDouble(string section, string key, double defaultValue, bool autoLog = true)
    {
        string v = GetString(section, key, null, autoLog);

        if (String.IsNullOrWhiteSpace(v))
        {
            return defaultValue;
        }

        try
        {
            return double.Parse(v, BasicTools.NFI_DOT);
        }
        catch (Exception e) when (e is FormatException || e is OverflowException)
        {
            return defaultValue;
        }
    }

    public decimal GetDecimal(string section, string key, decimal defaultValue, bool autoLog = true)
    {
        string v = GetString(section, key, null, autoLog);

        if (String.IsNullOrWhiteSpace(v))
        {
            return defaultValue;
        }

        try
        {
            return decimal.Parse(v, BasicTools.NFI_DOT);
        }
        catch (Exception e) when (e is FormatException || e is OverflowException)
        {
            return defaultValue;
        }
    }

    public TEnum GetEnum<TEnum>(string section, string key, TEnum defaultValue, bool autoLog = true) where TEnum : struct
    {
        string v = GetString(section, key, $"{defaultValue}", autoLog);
        return Enum.TryParse(v, true, out TEnum retVal) && Enum.IsDefined(typeof(TEnum), retVal)
            ? retVal
            : defaultValue;
    }
}

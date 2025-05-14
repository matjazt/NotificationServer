using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace SmoothLib;

/// <summary>
/// Provides a set of basic utility methods and properties for file operations, formatting, encryption, and application environment management.
/// </summary>
public static class BasicTools
{
    private static object _cs = new object();

    private static string assemblyDirectory;
    private static string assemblyName;
    private static string baseDirectory;
    private static bool? developmentMode;
    private static string appDataFolder;

    public static readonly NumberFormatInfo NFI_DOT = new NumberFormatInfo { NumberDecimalSeparator = "." };
    public static readonly NumberFormatInfo NFI_COMMA = new NumberFormatInfo { NumberDecimalSeparator = ",", NumberGroupSeparator = "." };

    public static readonly JsonSerializerOptions IndentedJsonSerializerOptions = new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        // Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static bool ServiceMode { get; set; }

    /// <summary>
    /// Provides the directory of the currently executing assembly.
    /// </summary>
    public static string AssemblyDirectory
    {
        get
        {
            lock (_cs)
            {
                assemblyDirectory ??= Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
                return assemblyDirectory;
            }
        }
    }

    /// <summary>
    /// Provides the name of the currently executing assembly without its extension.
    /// </summary>
    public static string AssemblyName
    {
        get
        {
            lock (_cs)
            {
                assemblyName ??= Path.GetFileNameWithoutExtension(Assembly.GetEntryAssembly().Location);
                return assemblyName;
            }
        }

        set
        {
            lock (_cs)
            {
                assemblyName = value;
            }
        }
    }

    /// <summary>
    /// Provides the base directory of the application, which is typically the parent directory of the "bin" folder.
    /// </summary>
    public static string BaseDirectory
    {
        get
        {
            lock (_cs)
            {
                if (baseDirectory == null)
                {
                    int binOffset = AssemblyDirectory.LastIndexOf($"{Path.DirectorySeparatorChar}bin", StringComparison.CurrentCultureIgnoreCase);
                    baseDirectory = binOffset < 0 ? AssemblyDirectory : AssemblyDirectory.Substring(0, binOffset);
                }

                return baseDirectory;
            }
        }
    }

    /// <summary>
    /// Provides a value indicating whether the application is running in development mode.
    /// </summary>
    public static bool DevelopmentMode
    {
        get
        {
            lock (_cs)
            {
                // if running in development mode, the base folder should include at least one csproj file and at least one of the obj and Properties subfolders
                developmentMode ??= (Directory.Exists(Path.Combine(BaseDirectory, "Properties")) || Directory.Exists(Path.Combine(BaseDirectory, "obj")))
                    && Directory.EnumerateFiles(BaseDirectory, "*.csproj").Any();
                return developmentMode.Value;
            }
        }

        set
        {
            lock (_cs)
            {
                developmentMode = value;
            }
        }
    }

    /// <summary>
    /// Provides the application data folder path, which is typically the "APPDATA" environment variable path combined with the assembly name.
    /// </summary>
    public static string AppDataFolder
    {
        get
        {
            lock (_cs)
            {
                appDataFolder ??= DevelopmentMode || ServiceMode ? BaseDirectory : Path.Combine(Environment.GetEnvironmentVariable("APPDATA"), AssemblyName);
                return appDataFolder;
            }
        }
    }

    /// <summary>
    /// Configures the default formats for numbers and dates, independent of the current culture.
    /// </summary>
    public static void SetDefaultFormats()
    {
        // set some default formats
        var culture = (CultureInfo)CultureInfo.CurrentCulture.Clone();
        culture.NumberFormat.NumberDecimalSeparator = ".";
        culture.DateTimeFormat.ShortDatePattern = "yyyy-MM-dd";
        culture.DateTimeFormat.LongDatePattern = "yyyy-MM-dd HH:mm:ss";
        Thread.CurrentThread.CurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentCulture = culture;
    }

    /// <summary>
    /// Sets the current working directory to the application data folder, creating it if necessary.
    /// </summary>
    public static void SetStartupFolder()
    {
        Directory.CreateDirectory(AppDataFolder);
        Directory.SetCurrentDirectory(AppDataFolder);
    }

    /// <summary>
    /// Saves a UTF-8 text file with a BOM (Byte Order Mark) to the specified file name.
    /// </summary>
    public static void SaveUtf8BomTextFile(string txt, string fileName)
    {
        using var mem = new MemoryStream();
        using (var sw = new StreamWriter(mem, new UTF8Encoding(true)))
        {
            sw.Write(txt);
        }

        SaveBinaryFile(mem.ToArray(), fileName);
    }

    /// <summary>
    /// Saves a binary file to the specified file name. It first writes to a temporary file and then renames it to the final file name.
    /// </summary>
    public static void SaveBinaryFile(byte[] content, string fileName)
    {
        string tempFileName = $"{fileName}.{Environment.TickCount}.{Environment.ProcessId}.temp";

        try
        {
            File.WriteAllBytes(tempFileName, content);
            File.Move(tempFileName, fileName, true);
            tempFileName = null;
        }
        finally
        {
            try
            {
                File.Delete(tempFileName);
            }
            catch { }
        }
    }

    /// <summary>
    /// Cross-platform version of Path.GetFileNameWithoutExtension (any path on any platform)
    /// Returns the file name without the extension from a given path string.
    /// </summary>
    /// <param name="s">path string</param>
    public static string GetFileNameWithoutExtension(string s)
    {
        int a = s.LastIndexOfAny(['\\', '/', ':']) + 1;
        int b = s.LastIndexOf('.', s.Length - 1, s.Length - a);
        b = b >= 0 ? (b - a) : (s.Length - a);
        return s.Substring(a, b);
    }

    // private static string _mainPassword;
    private static byte[] _mainAesKey;
    private static byte[] _mainAesInitializationVector;

    /// <summary>
    /// Sets the default password for the application. This password is used to derive the AES key and IV
    /// for encryption and decryption.
    /// </summary>
    public static void SetDefaultPassword(string password)
    {
        // _mainPassword = password;
        (_mainAesKey, _mainAesInitializationVector) = GetKeyAndIvFromUnsaltedPassword(password);
    }

    private static (byte[] key, byte[] iv) GetKeyAndIvFromUnsaltedPassword(string password)
    {
        // match the iteration count used in OpenSSL - 10000
        using var pbkdf2 = new Rfc2898DeriveBytes(password, [], 10000, HashAlgorithmName.SHA256);
        byte[] key = pbkdf2.GetBytes(32);  // AES-256 Key
        byte[] iv = pbkdf2.GetBytes(16);   // Initialization Vector (IV)
        return (key, iv);
    }

    private static Aes CreateAesFromUnsaltedPassword(string password)
    {
        var aes = Aes.Create();
        (aes.Key, aes.IV) = string.IsNullOrWhiteSpace(password) ? (_mainAesKey, _mainAesInitializationVector) : GetKeyAndIvFromUnsaltedPassword(password);
        return aes;
    }

    /// <summary>
    /// Returns the AES-256-CBC encrypted string of the given plain text using the specified password or the default password.
    /// OpenSSL equivalent: openssl enc -base64 -e -aes-256-cbc -pbkdf2 -nosalt -pass pass:SuperSecretPassword
    /// (you can ommit -pass and enter it interactively).
    /// </summary>
    public static string Aes256CbcEncrypt(string plainText, string password = null)
    {
        byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
        using var aes = CreateAesFromUnsaltedPassword(password);
        using var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
        byte[] encryptedBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);
        return Convert.ToBase64String(encryptedBytes);
    }

    /// <summary>
    /// Returns the plain text of the given AES-256-CBC encrypted string using the specified password or the default password.
    /// OpenSSL equivalent: openssl enc -base64 -d -aes-256-cbc -pbkdf2 -nosalt -pass pass:SuperSecretPassword
    /// (you can ommit -pass and enter it interactively).
    /// </summary>
    public static string Aes256CbcDecrypt(string base64CipherText, string password = null)
    {
        byte[] encryptedBytes = Convert.FromBase64String(base64CipherText);
        using var aes = CreateAesFromUnsaltedPassword(password);
        using var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
        byte[] decryptedBytes = decryptor.TransformFinalBlock(encryptedBytes, 0, encryptedBytes.Length);
        return Encoding.UTF8.GetString(decryptedBytes);
    }
}

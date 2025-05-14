using SmoothLib;

namespace UnitTests;

[Collection("Global Collection")]
public class SmoothLibTests
{
    [Fact]
    public void BasicToolsPropertiesTest()
    {
        Assert.NotEmpty(BasicTools.AppDataFolder);
        Assert.NotEmpty(BasicTools.AssemblyName);
        Assert.NotEmpty(BasicTools.AssemblyDirectory);
        Assert.NotEmpty(BasicTools.BaseDirectory);
        Assert.True(BasicTools.DevelopmentMode, "Development mode should be true in test environment.");
    }

    [Fact]
    public void GetFileNameWithoutExtensionTest()
    {
        // Windows style path with drive letter
        Assert.Equal("file", BasicTools.GetFileNameWithoutExtension(@"C:\folder\file.txt"));
        // Windows style path with backslashes
        Assert.Equal("archive.tar", BasicTools.GetFileNameWithoutExtension(@"folder\sub\archive.tar.gz"));
        // Unix style path with slashes
        Assert.Equal("document", BasicTools.GetFileNameWithoutExtension("/home/user/document.pdf"));
        // Path with no extension
        Assert.Equal("README", BasicTools.GetFileNameWithoutExtension(@"C:\project\README"));
        // Path with only file name
        Assert.Equal("notes", BasicTools.GetFileNameWithoutExtension("notes.txt"));
        // Path with only file name without extension
        Assert.Equal("šðžæè", BasicTools.GetFileNameWithoutExtension("šðžæè"));
        // Path with multiple dots
        Assert.Equal("my.file", BasicTools.GetFileNameWithoutExtension(@"folder\my.file.cs"));
        // Path with trailing slash (should return empty string)
        Assert.Equal(string.Empty, BasicTools.GetFileNameWithoutExtension(@"C:\folder\"));
    }

    [Fact]
    public void Aes256CbcEncryptDecrypt_RandomBlocks_Test()
    {
        var rng = new Random();

        for (int len = 1; len <= 130; len++)
        {
            // Generate random string of length 'len'
            char[] chars = new char[len];
            for (int i = 0; i < len; i++)
            {
                chars[i] = (char)rng.Next(1, 255);
            }

            string original = new string(chars);

            // generate a random password of semi random length
            string password = (original + Guid.NewGuid().ToString()).Substring(0, 1 + (len % 20));

            // Encrypt
            string encrypted = BasicTools.Aes256CbcEncrypt(original, password);
            Assert.False(string.IsNullOrEmpty(encrypted));

            // Decrypt
            string decrypted = BasicTools.Aes256CbcDecrypt(encrypted, password);
            Assert.Equal(original, decrypted);
        }
    }

    [Fact]
    public void ConfigTest()
    {
        // TODO: test encrypted strings and enums

        // Arrange: JSON with various types
        string json = """
        {
            "testSection": {
                "intValue": 42,
                "intValue2": "42",
                "stringValue": "hello",
                "boolValue1": true,
                "boolValue2": 0,
                "boolValue3": "1",
                "boolValue4": "false",
                "boolValue5": "rubbish",
                "doubleValue": 3.14,
                "int64Value": 9223372036854775807,
                "int64Value2": "-9223372036854775808",
                "int64Value3": -9223372036854775808,
                "uint32Value": 4294967295,
                "uint64Value": 18446744073709551615
            },
        }
        """;

        string section = "testSection";
        var config = Config.FromJsonText(json);

        // Act & Assert: Existing values
        Assert.Equal(42, config.GetInt32(section, "intValue", 0));
        Assert.Equal(42, config.GetInt32(section, "intValue2", 0));
        Assert.Equal(9223372036854775807L, config.GetInt64(section, "int64Value", 0));
        Assert.Equal(-9223372036854775808L, config.GetInt64(section, "int64Value2", 0));
        Assert.Equal(-9223372036854775808L, config.GetInt64(section, "int64Value3", 0));
        Assert.Equal(4294967295, config.GetUInt32(section, "uint32Value", 0));
        Assert.Equal(18446744073709551615, config.GetUInt64(section, "uint64Value", 0));

        Assert.Equal("hello", config.GetString(section, "stringValue"));

        Assert.True(config.GetBool(section, "boolValue1", false));
        Assert.False(config.GetBool(section, "boolValue2", true));
        Assert.True(config.GetBool(section, "boolValue3", false));
        Assert.False(config.GetBool(section, "boolValue4", true));
        Assert.True(config.GetBool(section, "boolValue5", true));
        Assert.False(config.GetBool(section, "boolValue5", false));

        Assert.Equal(3.14, config.GetDouble(section, "doubleValue", 0));
        Assert.Equal(3.14m, config.GetDecimal(section, "doubleValue", 0));

        // Non-existing values (should return defaults)
        Assert.Equal(42, config.GetInt32(section, "missing", 42));
        Assert.Equal(9223372036854775807L, config.GetInt64(section, "missing", 9223372036854775807L));
        Assert.Equal(uint.MaxValue, config.GetUInt32(section, "missing", uint.MaxValue));
        Assert.Equal(ulong.MaxValue, config.GetUInt64(section, "missing", ulong.MaxValue));
        Assert.Equal("hello", config.GetString(section, "missing", "hello"));
        Assert.Null(config.GetString(section, "missing"));
        Assert.True(config.GetBool(section, "missing", true));
        Assert.False(config.GetBool(section, "missing", false));
        Assert.Equal(3.14, config.GetDouble(section, "missing", 3.14));
        Assert.Equal(3.14m, config.GetDecimal(section, "missing", 3.14m));
    }
}

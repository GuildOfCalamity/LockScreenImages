using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Con = System.Diagnostics.Debug;

namespace LockScreenImages;

public class Profile : Serializer
{
    string title = string.Empty;
    public string Title 
    {
        get => title;
        set => title = value;
    }
    
    string lastCount = string.Empty;
    public string LastCount
    {
        get => lastCount;
        set => lastCount = value;
    }

    string apiKey = string.Empty;
    public string APIKey
    {
        get => apiKey;
        set => apiKey = value;
    }


    string lastUse = string.Empty;
    public string LastUse
    {
        get => lastUse;
        set => lastUse = value;
    }


    string positionX = string.Empty;
    public string PositionX
    {
        get => positionX;
        set => positionX = value;
    }


    string positionY = string.Empty;
    public string PositionY
    {
        get => positionY;
        set => positionY = value;
    }

    public Profile EncryptedCopy()
    {
        return new Profile
        {
            Title = EncryptionUtility.EncryptString(title),
            LastCount = EncryptionUtility.EncryptString(lastCount),
            APIKey = EncryptionUtility.EncryptString(apiKey),
            LastUse = EncryptionUtility.EncryptString(lastUse),
            PositionX = EncryptionUtility.EncryptString(positionX),
            PositionY = EncryptionUtility.EncryptString(positionY)
        };
    }

    public Profile DecryptedCopy()
    {
        return new Profile
        {
            Title = EncryptionUtility.DecryptString(title),
            LastCount = EncryptionUtility.DecryptString(lastCount),
            APIKey = EncryptionUtility.DecryptString(apiKey),
            LastUse = EncryptionUtility.DecryptString(lastUse),
            PositionX = EncryptionUtility.DecryptString(positionX),
            PositionY = EncryptionUtility.DecryptString(positionY)
        };
    }
}

public class Serializer
{
    #region [Generics]
    /// <summary>
    /// Read and deserialize file data into generic type.
    /// </summary>
    /// <example>
    /// Profile _profile = Serializer.Load<Profile>(System.IO.Path.Combine(Environment.CurrentDirectory, "profile.json"));
    /// </example>
    public static T? Load<T>(string path) where T : new()
    {
        try
        {
            if (File.Exists(path))
               return JsonSerializer.Deserialize<T>(File.ReadAllText(path));
            else
                return new T();
        }
        catch (Exception ex)
        {
            Con.WriteLine($"[Serializer.Load<T>]: {ex.Message}");
            return new T();
        }
    }

    /// <summary>
    /// Serialize an object type and write to file.
    /// </summary>
    /// <example>
    /// var _profile = new Profile { option1 = "thing1", option2 = "thing2" };
    /// _profile.Save(Path.Combine(Environment.CurrentDirectory, "profile.json"));
    /// </example>
    public static bool Save<T>(string path, T obj) where T : new()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? AppDomain.CurrentDomain.BaseDirectory);
            if (typeof(T) == typeof(Profile))
            {
                File.WriteAllText(path, JsonSerializer.Serialize((obj as Profile)?.EncryptedCopy(), typeof(T), new JsonSerializerOptions { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull, WriteIndented = true, PropertyNameCaseInsensitive = true }));
            }
            else
            {
                File.WriteAllText(path, JsonSerializer.Serialize(obj, typeof(T), new JsonSerializerOptions { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull, WriteIndented = true, PropertyNameCaseInsensitive = true }));
            }
            return true;
        }
        catch (Exception ex)
        {
            Con.WriteLine($"[Serializer.Save<T>]: {ex.Message}");
            return false;
        }
    }
    #endregion

    /// <summary>
    /// Serialize a <see cref="Profile"/> object and write to file.
    /// </summary>
    /// <example>
    /// var _profile = new Profile { option1 = "thing1", option2 = "thing2" };
    /// _profile.Load(Path.Combine(Environment.CurrentDirectory, "profile.json"));
    /// </example>
    public Profile? Load(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                var tmp = JsonSerializer.Deserialize<Profile>(File.ReadAllText(path));
                return tmp?.DecryptedCopy();
            }
            return null;
        }
        catch (Exception ex)
        {
            Con.WriteLine($"[Serializer.Load]: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Serialize a <see cref="Profile"/> object and write to file.
    /// </summary>
    /// <example>
    /// var _profile = new Profile { option1 = "thing1", option2 = "thing2" };
    /// _profile.Save(Path.Combine(Environment.CurrentDirectory, "profile.json"));
    /// </example>
    public bool Save(string path, Profile profile)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? AppDomain.CurrentDomain.BaseDirectory);
            File.WriteAllText(path, JsonSerializer.Serialize(profile.EncryptedCopy(), typeof(Profile), new JsonSerializerOptions { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull, WriteIndented = true, PropertyNameCaseInsensitive = true }));
            return true;
        }
        catch (Exception ex)
        {
            Con.WriteLine($"[Serializer.Save]: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Serializes a <see cref="Profile"/> instance and writes to file.
    /// </summary>
    /// <example>
    /// var _profile = new Profile { option1 = "thing1", option2 = "thing2" };
    /// _profile.Save(Path.Combine(Environment.CurrentDirectory, "profile.json"));
    /// </example>
    public bool Save(string path)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? AppDomain.CurrentDomain.BaseDirectory);
            File.WriteAllText(path, JsonSerializer.Serialize(this, typeof(Profile), new JsonSerializerOptions { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull, WriteIndented = true, PropertyNameCaseInsensitive = true }));
            return true;
        }
        catch (Exception ex)
        {
            Con.WriteLine($"[Serializer.Save]: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Test method for returning multiple profiles based on a matched setting.
    /// </summary>
    /// <param name="path">file path</param>
    /// <returns><see cref="List{T}"/></returns>
    public static List<Profile> GetActiveAPIKeyProfiles(string path)
    {
        try
        {
            var profs = JsonSerializer.Deserialize<List<Profile>>(File.ReadAllText(path)).Where(o => !string.IsNullOrEmpty(o.APIKey)).ToList();
            return profs;
        }
        catch (Exception ex)
        {
            Con.WriteLine($"[Serializer.GetActives]: {ex.Message}");
            return new List<Profile>();
        }
    }
}

public static class EncryptionUtility
{
    static readonly string p1 = "Rubber";
    static readonly string p2 = "Bumpers";
    static readonly string p3 = "Baby";
    static readonly byte[] _entropyBytes = Encoding.UTF8.GetBytes($"{p1}{p3}{p2}");

    /// <summary>
    /// Utilizes <see cref="DataProtectionScope.LocalMachine"/> for the encryption process.
    /// </summary>
    /// <param name="unencryptedString">the clear text to encrypt</param>
    /// <returns>Base64 encrypted string</returns>
    public static string EncryptString(string unencryptedString)
    {
        if (string.IsNullOrEmpty(unencryptedString))
            return string.Empty;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(unencryptedString), _entropyBytes, DataProtectionScope.LocalMachine));
        }
        else
        {
            return FallbackEncryptDecrypt(unencryptedString, $"{p1}{p3}{p2}");
        }
    }

    /// <summary>
    /// Utilizes <see cref="DataProtectionScope.LocalMachine"/> for the decryption process.
    /// </summary>
    /// <param name="encryptedString">the encrypted text to decipher</param>
    /// <returns>unencrypted string</returns>
    public static string DecryptString(string encryptedString)
    {
        if (string.IsNullOrEmpty(encryptedString))
            return string.Empty;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            byte[] bytes = ProtectedData.Unprotect(Convert.FromBase64String(encryptedString), _entropyBytes, DataProtectionScope.LocalMachine);
            return Encoding.UTF8.GetString(bytes);
        }
        else
        {
            return FallbackEncryptDecrypt(encryptedString, $"{p1}{p3}{p2}");
        }
    }

    /// <summary>
    /// Not secure, but better than clear text.
    /// </summary>
    static string FallbackEncryptDecrypt(string input, string key)
    {
        if (string.IsNullOrEmpty(input))
            return string.Empty;

        StringBuilder output = new StringBuilder();
        int keyLength = key.Length;
        for (int i = 0; i < input.Length; i++)
        {
            char encryptedChar = (char)(input[i] ^ key[i % keyLength]);
            output.Append(encryptedChar);
        }
        return $"{output}";
    }
}


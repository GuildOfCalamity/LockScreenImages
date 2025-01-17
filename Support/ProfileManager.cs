﻿using Con = System.Diagnostics.Debug;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LockScreenImages;

// Custom attribute to mark sensitive properties
[AttributeUsage(AttributeTargets.Property)]
public class EncryptedAttribute : Attribute { }

// Using custom attribute
public class AppSettings
{
    #region [Public Members]
    public string? Username { get; set; }
    [Encrypted]
    public string? Password { get; set; }
    [Encrypted]
    public string? ApiSecret { get; set; }
    public string? ApiKey { get; set; }
    public DateTime? LastUse { get; set; }
    public int WindowTop { get; set; }
    public int WindowLeft { get; set; }
    public int WindowWidth { get; set; }
    public int WindowHeight { get; set; }
    public int LastCount    { get; set; }
    public int InactivityTimeout { get; set; }
    public int WindowState { get; set; }
    public string? StartupPosition { get; set; }
    public string? Version { get; set; }
    public string? Theme { get; set; }
    public bool GlassWindow { get; set; } = true;
    public bool FirstRun { get; set; } = true;
    public bool DebugMode { get; set; }

    #endregion

    #region [Private Members]
    static bool portable = false;
    static readonly string p1 = "Rubber";
    static readonly string p2 = "Bumper";
    static readonly string p3 = "Baby";
    static readonly byte[] entropyBytes = Encoding.UTF8.GetBytes($"{p1}{p3}{p2}");
    static readonly string SettingsFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "profile.json");
    #endregion

    /// <summary>
    /// Load settings profile with automatic decryption.
    /// </summary>
    /// <param name="portableEncryption">
    /// <para>If true, the profile will use a standard <see cref="System.Security.Cryptography.Aes"/>-128 encryption process for identified properties, and the
    /// settings will work if transfered to another machine.</para>
    /// <para>If false, the profile will use <see cref="System.Security.Cryptography.ProtectedData"/> for encryption, 
    /// but this will only be compatible machine-wide and is incompatible once copied to another machine.</para>
    /// </param>
    /// <returns><see cref="AppSettings"/> object</returns>
    public static AppSettings Load(bool portableEncryption)
    {
        portable = portableEncryption;
        if (!File.Exists(SettingsFilePath))
        {
            Con.WriteLine("[ERROR] Settings file not found, returning a new instance.");
            return new AppSettings();
        }

        try
        {
            var json = File.ReadAllText(SettingsFilePath);
            var settings = JsonSerializer.Deserialize<AppSettings>(json, new JsonSerializerOptions
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                WriteIndented = true,
                PropertyNameCaseInsensitive = true
            });
            settings?.DecryptSensitiveProperties();
            //settings?.ValidateEncryptedProperties(false); // Validate after loading (decrypted state)
            Con.WriteLine("[INFO] Settings successfully loaded.");
            return settings ?? new AppSettings();
        }
        catch (Exception ex)
        {
            Con.WriteLine($"[ERROR] Loading settings: {ex.Message}");
            return new AppSettings();
        }
    }

    /// <summary>
    /// Save settings profile with automatic encryption. 
    /// After <see cref="AppSettings.Load(bool)"/> is called, the 'portable' setting is observed during encryption/decryption.
    /// </summary>
    public void Save()
    {
        try
        {
            //ValidateEncryptedProperties(true); // Validate before saving (encrypted state)
            EncryptSensitiveProperties();
            var json = JsonSerializer.Serialize(this, new JsonSerializerOptions
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                WriteIndented = true,
                PropertyNameCaseInsensitive = true
            });
            File.WriteAllText(SettingsFilePath, json);
            Con.WriteLine("[INFO] Settings successfully saved.");
        }
        catch (Exception ex)
        {
            Con.WriteLine($"[ERROR] saving settings: {ex.Message}");
        }
    }

    /// <summary>
    /// Encrypt all properties marked with [Encrypted]
    /// </summary>
    void EncryptSensitiveProperties()
    {
        foreach (var property in GetType().GetProperties())
        {
            if (Attribute.IsDefined(property, typeof(EncryptedAttribute)))
            {
                var value = property.GetValue(this)?.ToString();
                if (!string.IsNullOrEmpty(value))
                {
                    if (!IsEncrypted(value))
                    {
                        property.SetValue(this, Encrypt(value));
                    }
                    else
                    {
                        Con.WriteLine($"[WARNING] Property '{property.Name}' is already encrypted.");
                    }
                }
            }
        }
    }

    /// <summary>
    /// Decrypt all properties marked with [Encrypted]
    /// </summary>
    void DecryptSensitiveProperties()
    {
        foreach (var property in GetType().GetProperties())
        {
            if (Attribute.IsDefined(property, typeof(EncryptedAttribute)))
            {
                var value = property.GetValue(this)?.ToString();
                if (!string.IsNullOrEmpty(value))
                {
                    if (IsEncrypted(value))
                        property.SetValue(this, Decrypt(value));
                    else
                        property.SetValue(this, value);
                }
            }
        }
    }

    /// <summary>
    /// Validate properties marked with [Encrypted]
    /// </summary>
    /// <param name="isEncrypted"></param>
    /// <exception cref="InvalidOperationException"></exception>
    void ValidateEncryptedProperties(bool isEncrypted)
    {
        foreach (var property in GetType().GetProperties())
        {
            if (Attribute.IsDefined(property, typeof(EncryptedAttribute)))
            {
                var value = property.GetValue(this)?.ToString();
                if (!string.IsNullOrEmpty(value))
                {
                    if (isEncrypted && !IsEncrypted(value))
                    {
                        throw new InvalidOperationException($"Property '{property.Name}' is not encrypted but should be.");
                    }

                    if (!isEncrypted && IsEncrypted(value))
                    {
                        throw new InvalidOperationException($"Property '{property.Name}' is encrypted but should be plain text.");
                    }
                }
            }
        }
    }

    /// <summary>
    /// Attempt to decode and decrypt. If it fails, it's not valid encrypted data.
    /// </summary>
    static bool IsEncrypted(string value)
    {
        try
        {
            if (!portable)
            {
                var bytes = Convert.FromBase64String(value);
                _ = ProtectedData.Unprotect(bytes, entropyBytes, DataProtectionScope.LocalMachine);
                return true;
            }
            else
            {
                return IsAesEncrypted(value);
            }
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// For portable encryption checking
    /// </summary>
    static bool IsAesEncrypted(string input)
    {
        if (string.IsNullOrEmpty(input))
            return false;

        byte[] decodedBytes;
        try
        {
            decodedBytes = Convert.FromBase64String(input);
        }
        catch (FormatException)
        {
            return false; // Invalid Base64 string
        }

        // Check if the length is a multiple of 16 (AES block size)
        if (decodedBytes.Length % 16 != 0)
            return false; // Not an AES block size

        //if (!input.EndsWith("=")) { return false; }

        return true; // Likely AES or Base64
    }

    /// <summary>
    /// Encrypt a string
    /// </summary>
    /// <param name="plainText"></param>
    /// <returns></returns>
    static string Encrypt(string plainText)
    {
        if (string.IsNullOrEmpty(plainText))
            return string.Empty;

        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && !portable)
            {
                var plainBytes = Encoding.UTF8.GetBytes(plainText);
                var encryptedBytes = ProtectedData.Protect(plainBytes, entropyBytes, DataProtectionScope.LocalMachine);
                return Convert.ToBase64String(encryptedBytes);
            }
            else
            {
                return EncryptPortable(plainText, $"{p1}{p3}{p2}");
            }
        }
        catch (Exception ex)
        {
            Con.WriteLine($"[ERROR] Encrypting data: {ex.Message}");
            return string.Empty;
        }
    }

    /// <summary>
    /// Decrypt a string
    /// </summary>
    /// <param name="encryptedText"></param>
    /// <returns></returns>
    static string Decrypt(string encryptedText)
    {
        if (string.IsNullOrEmpty(encryptedText))
            return string.Empty;

        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && !portable)
            {
                var encryptedBytes = Convert.FromBase64String(encryptedText);
                var plainBytes = ProtectedData.Unprotect(encryptedBytes, entropyBytes, DataProtectionScope.LocalMachine);
                return Encoding.UTF8.GetString(plainBytes);
            }
            else
            {
                return DecryptPortable(encryptedText, $"{p1}{p3}{p2}");
            }
        }
        catch (Exception ex)
        {
            Con.WriteLine($"[ERROR] Decrypting data: {ex.Message}");
            return string.Empty;
        }
    }

    /// <summary>
    /// Portable method to encrypt a string
    /// </summary>
    /// <param name="plainText"></param>
    /// <returns>encrypted text</returns>
    static string EncryptPortable(string plainText, string hash)
    {
        using (Aes aes = Aes.Create())
        {
            aes.Key = Encoding.UTF8.GetBytes($"{hash}"); // Must be 16 bytes for AES-128
            aes.IV = Encoding.UTF8.GetBytes($"{hash}");  // Must be 16 bytes
            using (var encryptor = aes.CreateEncryptor(aes.Key, aes.IV))
            {
                using (var memoryStream = new MemoryStream())
                {
                    using (var cryptoStream = new CryptoStream(memoryStream, encryptor, CryptoStreamMode.Write))
                    {
                        using (var writer = new StreamWriter(cryptoStream))
                        {
                            writer.Write(plainText);
                        }
                        return Convert.ToBase64String(memoryStream.ToArray());
                    }
                }
            }
        }
    }

    /// <summary>
    /// Portable method to decrypt a string
    /// </summary>
    /// <param name="cipherText"></param>
    /// <returns>decrypted text</returns>
    static string DecryptPortable(string cipherText, string hash)
    {
        using (Aes aes = Aes.Create())
        {
            aes.Key = Encoding.UTF8.GetBytes($"{hash}"); // Must be 16 bytes for AES-128
            aes.IV = Encoding.UTF8.GetBytes($"{hash}");  // Must be 16 bytes
            using (var decryptor = aes.CreateDecryptor(aes.Key, aes.IV))
            {
                using (var memoryStream = new MemoryStream(Convert.FromBase64String(cipherText)))
                {
                    using (var cryptoStream = new CryptoStream(memoryStream, decryptor, CryptoStreamMode.Read))
                    using (var reader = new StreamReader(cryptoStream))
                    {
                        return reader.ReadToEnd();
                    }
                }
            }
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

/* [EncryptionUtility Class]

public static class EncryptionUtility
{
    #region [Private Members]
    static readonly string p1 = "Rubber_";
    static readonly string p2 = "Bumpers_";
    static readonly string p3 = "Baby_";
    static readonly byte[] _entropyBytes = Encoding.UTF8.GetBytes($"{p1}{p3}{p2}");
    #endregion

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

*/
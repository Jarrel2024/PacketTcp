using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace PacketTcp.Cryptos;

/// <summary>
/// Provides encryption and decryption functionality using the AES algorithm.
/// </summary>
public class AESCryptoProvider : ICryptoProvider, IDisposable
{
    /// <summary>
    /// Gets or sets the encryption key in Base64 format.
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the initialization vector (IV) in Base64 format.
    /// </summary>
    public string IV { get; set; } = string.Empty;

    private readonly Aes aes = Aes.Create();

    /// <summary>
    /// Generates the encryption key and initialization vector (IV) using a user-provided password and salt.
    /// </summary>
    /// <param name="password">The password provided by the user.</param>
    /// <param name="salt">The salt value used to enhance security.</param>
    public void GenerateKeysFromPassword(string password, string salt)
    {
        using var keyDerivation = new Rfc2898DeriveBytes(password, Encoding.UTF8.GetBytes(salt), 100000, HashAlgorithmName.SHA256);
        aes.Key = keyDerivation.GetBytes(32); // 256-bit key
        aes.IV = keyDerivation.GetBytes(16); // 128-bit IV
        Key = Convert.ToBase64String(aes.Key);
        IV = Convert.ToBase64String(aes.IV);
    }

    /// <summary>
    /// Randomly generates the encryption key and initialization vector (IV).
    /// </summary>
    public void GenerateKeys()
    {
        aes.GenerateKey();
        aes.GenerateIV();
        Key = Convert.ToBase64String(aes.Key);
        IV = Convert.ToBase64String(aes.IV);
    }

    /// <summary>
    /// Configures the AES encryption settings using a Base64-encoded key and initialization vector (IV).
    /// </summary>
    /// <param name="key">The encryption key in Base64 format.</param>
    /// <param name="iv">The initialization vector (IV) in Base64 format.</param>
    public void FromBase64String(string key, string iv)
    {
        Key = key;
        IV = iv;
        aes.Key = Convert.FromBase64String(key);
        aes.IV = Convert.FromBase64String(iv);
    }

    public byte[] Encrypt(byte[] data)
    {
        using var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
        using var ms = new MemoryStream();
        using var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write);
        cs.Write(data, 0, data.Length);
        cs.Close();
        return ms.ToArray();
    }

    public byte[] Decrypt(byte[] data)
    {
        using var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
        using var ms = new MemoryStream(data);
        using var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read);
        byte[] buffer = new byte[data.Length];
        int readCount = cs.Read(buffer, 0, buffer.Length);
        Array.Resize(ref buffer, readCount);
        return buffer;
    }

    /// <summary>
    /// Releases all resources used by the AES encryption service.
    /// </summary>
    public void Dispose()
    {
        aes.Dispose();
    }
}

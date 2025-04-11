namespace PacketTcp.Cryptos;
public interface ICryptoProvider
{
    /// <summary>
    /// Encrypts the specified data using the AES algorithm.
    /// </summary>
    /// <param name="data">The data to encrypt as a byte array.</param>
    /// <returns>The encrypted data as a byte array.</returns>
    public byte[] Encrypt(byte[] data);
    /// <summary>
    /// Decrypts the specified data using the AES algorithm.
    /// </summary>
    /// <param name="data">The encrypted data as a byte array.</param>
    /// <returns>The decrypted data as a byte array.</returns>
    public byte[] Decrypt(byte[] data);
}

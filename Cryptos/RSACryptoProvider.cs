using System.Security.Cryptography;

namespace PacketTcp.Cryptos;
public class RSACryptoProvider : ICryptoProvider, IDisposable
{
    public string PublicKey { get; set; } = string.Empty;
    public string PrivateKey { get; set; } = string.Empty;
    private readonly RSA rsa = RSA.Create();
    public void GenerateKeys()
    {
        using (var rsa = new System.Security.Cryptography.RSACryptoServiceProvider(2048))
        {
            PublicKey = rsa.ToXmlString(false);
            PrivateKey = rsa.ToXmlString(true);
        }
    }

    public void FromXmlString(string publicKey, string privateKey)
    {
        PublicKey = publicKey;
        PrivateKey = privateKey;
    }

    public byte[] Encrypt(byte[] data)
    {
        rsa.FromXmlString(PublicKey);
        return rsa.Encrypt(data, RSAEncryptionPadding.Pkcs1);
    }

    public byte[] Decrypt(byte[] data)
    {
        rsa.FromXmlString(PrivateKey);
        return rsa.Decrypt(data, RSAEncryptionPadding.Pkcs1);
    }

    public void Dispose()
    {
        rsa.Dispose();
    }
}

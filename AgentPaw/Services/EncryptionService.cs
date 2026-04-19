using System.Security.Cryptography;
using System.Text;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Modes;
using Org.BouncyCastle.Crypto.Parameters;

namespace AgentPaw.Services;

/// <summary>
/// AES-256-GCM 암호화. 기존 Node.js 버전과 동일한 키 파생 및 포맷(iv:tag:ciphertext) 사용.
/// Node.js crypto는 IV 16바이트를 허용하지만 .NET AesGcm은 12바이트만 허용한다.
/// BouncyCastle을 사용하여 양쪽 모두 호환한다.
/// </summary>
public class EncryptionService
{
    private readonly byte[] _key;
    private const int IvLength = 16;   // Node.js 호환 (16바이트)
    private const int TagBits = 128;   // 16바이트 = 128비트

    public EncryptionService()
    {
        var hostname = Environment.MachineName;
        var username = Environment.UserName;
        var seed = $"agent-paw-{hostname}-{username}-encryption-key-v1";
        _key = SHA256.HashData(Encoding.UTF8.GetBytes(seed));
    }

    public string Encrypt(string plaintext)
    {
        var iv = RandomNumberGenerator.GetBytes(IvLength);
        var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);

        var cipher = new GcmBlockCipher(new AesEngine());
        cipher.Init(true, new AeadParameters(new KeyParameter(_key), TagBits, iv));

        var output = new byte[cipher.GetOutputSize(plaintextBytes.Length)];
        var len = cipher.ProcessBytes(plaintextBytes, 0, plaintextBytes.Length, output, 0);
        cipher.DoFinal(output, len);

        // output = ciphertext + tag (마지막 16바이트가 tag)
        var tagLength = TagBits / 8;
        var ciphertext = output[..^tagLength];
        var tag = output[^tagLength..];

        return $"{Convert.ToHexString(iv).ToLower()}:{Convert.ToHexString(tag).ToLower()}:{Convert.ToHexString(ciphertext).ToLower()}";
    }

    public string Decrypt(string encrypted)
    {
        var parts = encrypted.Split(':');
        if (parts.Length != 3)
            throw new FormatException("Invalid encrypted format. Expected iv:tag:ciphertext");

        var iv = Convert.FromHexString(parts[0]);
        var tag = Convert.FromHexString(parts[1]);
        var ciphertext = Convert.FromHexString(parts[2]);

        // BouncyCastle expects ciphertext + tag concatenated
        var input = new byte[ciphertext.Length + tag.Length];
        Buffer.BlockCopy(ciphertext, 0, input, 0, ciphertext.Length);
        Buffer.BlockCopy(tag, 0, input, ciphertext.Length, tag.Length);

        var cipher = new GcmBlockCipher(new AesEngine());
        cipher.Init(false, new AeadParameters(new KeyParameter(_key), TagBits, iv));

        var output = new byte[cipher.GetOutputSize(input.Length)];
        var len = cipher.ProcessBytes(input, 0, input.Length, output, 0);
        cipher.DoFinal(output, len);

        return Encoding.UTF8.GetString(output);
    }
}

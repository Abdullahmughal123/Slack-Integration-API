using System.Security.Cryptography;
using System.Text;
using SlackIntegration.Interfaces;
using SlackIntegration.Exceptions;

namespace SlackIntegration.Services;

public class EncryptionService : IEncryptionService
{
    private readonly IConfiguration _config;
    private readonly ILogger<EncryptionService> _logger;

    public EncryptionService(IConfiguration config, ILogger<EncryptionService> logger)
    {
        _config = config;
        _logger = logger;
    }

    public string Encrypt(string plainText)
    {
        try
        {
            if (string.IsNullOrEmpty(plainText))
            {
                throw new SlackIntegrationException("Plain text cannot be null or empty", "ENCRYPT_INPUT_EMPTY");
            }

            var key = GetEncryptionKey();
            var iv = GetEncryptionIV();

            using var aes = Aes.Create();
            aes.Key = key;
            aes.IV = iv;

            using var encryptor = aes.CreateEncryptor();
            var bytes = Encoding.UTF8.GetBytes(plainText);

            var encrypted = encryptor.TransformFinalBlock(bytes, 0, bytes.Length);

            return Convert.ToBase64String(encrypted);
        }
        catch (Exception ex) when (!(ex is SlackIntegrationException))
        {
            _logger.LogError(ex, "Failed to encrypt data");
            throw new SlackIntegrationException("Encryption failed", "ENCRYPT_ERROR", ex);
        }
    }

    public string Decrypt(string cipherText)
    {
        try
        {
            if (string.IsNullOrEmpty(cipherText))
            {
                throw new SlackIntegrationException("Cipher text cannot be null or empty", "DECRYPT_INPUT_EMPTY");
            }

            var key = GetEncryptionKey();
            var iv = GetEncryptionIV();

            using var aes = Aes.Create();
            aes.Key = key;
            aes.IV = iv;

            using var decryptor = aes.CreateDecryptor();
            var bytes = Convert.FromBase64String(cipherText);

            var decrypted = decryptor.TransformFinalBlock(bytes, 0, bytes.Length);

            return Encoding.UTF8.GetString(decrypted);
        }
        catch (Exception ex) when (!(ex is SlackIntegrationException))
        {
            _logger.LogError(ex, "Failed to decrypt data");
            throw new SlackIntegrationException("Decryption failed", "DECRYPT_ERROR", ex);
        }
    }

    private byte[] GetEncryptionKey()
    {
        var key = _config["Encryption:Key"];
        if (string.IsNullOrEmpty(key))
        {
            throw new SlackIntegrationException("Encryption key not configured", "ENCRYPT_KEY_MISSING");
        }

        return Encoding.UTF8.GetBytes(key);
    }

    private byte[] GetEncryptionIV()
    {
        var iv = _config["Encryption:IV"];
        if (string.IsNullOrEmpty(iv))
        {
            throw new SlackIntegrationException("Encryption IV not configured", "ENCRYPT_IV_MISSING");
        }

        return Encoding.UTF8.GetBytes(iv);
    }
}
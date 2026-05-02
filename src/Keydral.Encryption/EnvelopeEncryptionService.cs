using System.Security.Cryptography;
using Keydral.Encryption.Models;
using Keydral.Encryption.Providers;

namespace Keydral.Encryption;

/// <summary>
/// Encryption service implementing envelope encryption.
/// - Master key encrypts data encryption keys (DEKs)
/// - DEKs encrypt actual secret values
/// </summary>
public interface IEncryptionService
{
    /// <summary>
    /// Encrypt a secret value using envelope encryption.
    /// </summary>
    Task<EncryptedSecret> EncryptAsync(
        string secretValue,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Decrypt an encrypted secret.
    /// </summary>
    Task<string> DecryptAsync(
        EncryptedSecret encryptedSecret,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generate a fresh data encryption key and encrypt it with the master key.
    /// </summary>
    Task<DataEncryptionKey> GenerateDataEncryptionKeyAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Decrypt a data encryption key using the master key.
    /// </summary>
    Task<DataEncryptionKey> DecryptDataEncryptionKeyAsync(
        byte[] encryptedKeyBytes,
        byte[] iv,
        byte[] authTag,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Default implementation of envelope encryption using AES-256-GCM.
/// </summary>
public class EnvelopeEncryptionService : IEncryptionService
{
    private readonly IMasterKeyProvider _masterKeyProvider;
    private const int KeySizeInBytes = 32; // 256 bits
    private const int IVSizeInBytes = 12; // 96 bits (standard for GCM)
    private const int TagSizeInBytes = 16; // 128 bits (GCM tag)

    /// <summary>
    /// Constructor.
    /// </summary>
    public EnvelopeEncryptionService(IMasterKeyProvider masterKeyProvider)
    {
        _masterKeyProvider = masterKeyProvider ?? throw new ArgumentNullException(nameof(masterKeyProvider));
    }

    /// <summary>
    /// Encrypt a secret value using envelope encryption.
    /// 1. Generate a random data encryption key (DEK)
    /// 2. Encrypt the DEK with the master key
    /// 3. Encrypt the secret with the DEK
    /// 4. Return encrypted secret + encrypted DEK + IVs + auth tags
    /// </summary>
    public async Task<EncryptedSecret> EncryptAsync(
        string secretValue,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(secretValue))
            throw new ArgumentException("Secret value cannot be empty", nameof(secretValue));

        // Generate a fresh data encryption key
        var dek = await GenerateDataEncryptionKeyAsync(cancellationToken);

        try
        {
            // Encrypt the secret with the DEK
            var (encryptedData, dataIv, dataTag) = EncryptWithDek(secretValue, dek.KeyBytes);

            return new EncryptedSecret(
                encryptedData: Convert.ToBase64String(encryptedData),
                encryptedDataKey: Convert.ToBase64String(dek.EncryptedKeyBytes),
                initializationVector: Convert.ToBase64String(dataIv),
                authenticationTag: Convert.ToBase64String(dataTag),
                keyInitializationVector: Convert.ToBase64String(dek.InitializationVector),
                keyAuthenticationTag: Convert.ToBase64String(dek.AuthenticationTag),
                encryptionKeyId: dek.KeyId);
        }
        finally
        {
            // Secure cleanup
            dek.ZeroKeyBytes();
        }
    }

    /// <summary>
    /// Decrypt an encrypted secret.
    /// 1. Decrypt the DEK using the master key
    /// 2. Decrypt the secret using the DEK
    /// </summary>
    public async Task<string> DecryptAsync(
        EncryptedSecret encryptedSecret,
        CancellationToken cancellationToken = default)
    {
        if (encryptedSecret == null)
            throw new ArgumentNullException(nameof(encryptedSecret));

        var encryptedKeyBytes = Convert.FromBase64String(encryptedSecret.EncryptedDataKey);
        var keyIv = Convert.FromBase64String(encryptedSecret.KeyInitializationVector);
        var keyAuthTag = Convert.FromBase64String(encryptedSecret.KeyAuthenticationTag);

        // Decrypt the data encryption key
        var dek = await DecryptDataEncryptionKeyAsync(encryptedKeyBytes, keyIv, keyAuthTag, cancellationToken);

        try
        {
            // Decrypt the secret with the DEK
            var encryptedData = Convert.FromBase64String(encryptedSecret.EncryptedData);
            var dataIv = Convert.FromBase64String(encryptedSecret.InitializationVector);
            var dataAuthTag = Convert.FromBase64String(encryptedSecret.AuthenticationTag);

            return DecryptWithDek(encryptedData, dek.KeyBytes, dataIv, dataAuthTag);
        }
        finally
        {
            // Secure cleanup
            dek.ZeroKeyBytes();
        }
    }

    /// <summary>
    /// Generate a fresh data encryption key and encrypt it with the master key.
    /// </summary>
    public async Task<DataEncryptionKey> GenerateDataEncryptionKeyAsync(
        CancellationToken cancellationToken = default)
    {
        // Generate a random DEK
        using var rng = RandomNumberGenerator.Create();
        var dekBytes = new byte[KeySizeInBytes];
        rng.GetBytes(dekBytes);

        // Encrypt the DEK with the master key
        var masterKey = await _masterKeyProvider.GetMasterKeyAsync(cancellationToken);
        var (encryptedDek, dekIv, dekTag) = EncryptWithMasterKey(dekBytes, masterKey);

        return new DataEncryptionKey(dekBytes, encryptedDek, dekIv, dekTag);
    }

    /// <summary>
    /// Decrypt a data encryption key using the master key.
    /// </summary>
    public async Task<DataEncryptionKey> DecryptDataEncryptionKeyAsync(
        byte[] encryptedKeyBytes,
        byte[] iv,
        byte[] authTag,
        CancellationToken cancellationToken = default)
    {
        if (encryptedKeyBytes == null || encryptedKeyBytes.Length == 0)
            throw new ArgumentException("Encrypted key bytes cannot be empty", nameof(encryptedKeyBytes));
        if (iv == null || iv.Length != IVSizeInBytes)
            throw new ArgumentException($"IV must be {IVSizeInBytes} bytes", nameof(iv));
        if (authTag == null || authTag.Length != TagSizeInBytes)
            throw new ArgumentException($"Auth tag must be {TagSizeInBytes} bytes", nameof(authTag));

        var masterKey = await _masterKeyProvider.GetMasterKeyAsync(cancellationToken);

        try
        {
            var dekBytes = DecryptWithMasterKey(encryptedKeyBytes, masterKey, iv, authTag);
            return new DataEncryptionKey(dekBytes, encryptedKeyBytes, iv, authTag);
        }
        finally
        {
            // Note: We don't zero the master key here because the provider may be caching it.
            // The provider is responsible for securing the master key.
            // Zeroing it here could corrupt cached keys in providers like FileBasedMasterKeyProvider.
        }
    }

    /// <summary>
    /// Encrypt data with the data encryption key using AES-256-GCM.
    /// </summary>
    private static (byte[] ciphertext, byte[] iv, byte[] authTag) EncryptWithDek(
        string plaintext,
        byte[] dek)
    {
        using (var cipher = new AesGcm(dek, TagSizeInBytes))
        {
            using var rng = RandomNumberGenerator.Create();
            var iv = new byte[IVSizeInBytes];
            rng.GetBytes(iv);

            var plaintextBytes = System.Text.Encoding.UTF8.GetBytes(plaintext);
            var ciphertext = new byte[plaintextBytes.Length];
            var authTag = new byte[TagSizeInBytes];

            cipher.Encrypt(iv, plaintextBytes, ciphertext, authTag);

            return (ciphertext, iv, authTag);
        }
    }

    /// <summary>
    /// Decrypt data using the data encryption key.
    /// </summary>
    private static string DecryptWithDek(
        byte[] ciphertext,
        byte[] dek,
        byte[] iv,
        byte[] authTag)
    {
        using (var cipher = new AesGcm(dek, TagSizeInBytes))
        {
            var plaintext = new byte[ciphertext.Length];
            cipher.Decrypt(iv, ciphertext, authTag, plaintext);
            return System.Text.Encoding.UTF8.GetString(plaintext);
        }
    }

    /// <summary>
    /// Encrypt the data encryption key with the master key.
    /// </summary>
    private static (byte[] ciphertext, byte[] iv, byte[] authTag) EncryptWithMasterKey(
        byte[] dek,
        byte[] masterKey)
    {
        using (var cipher = new AesGcm(masterKey, TagSizeInBytes))
        {
            using var rng = RandomNumberGenerator.Create();
            var iv = new byte[IVSizeInBytes];
            rng.GetBytes(iv);

            var ciphertext = new byte[dek.Length];
            var authTag = new byte[TagSizeInBytes];

            cipher.Encrypt(iv, dek, ciphertext, authTag);

            return (ciphertext, iv, authTag);
        }
    }

    /// <summary>
    /// Decrypt the data encryption key using the master key.
    /// </summary>
    private static byte[] DecryptWithMasterKey(
        byte[] ciphertext,
        byte[] masterKey,
        byte[] iv,
        byte[] authTag)
    {
        using (var cipher = new AesGcm(masterKey, TagSizeInBytes))
        {
            var plaintext = new byte[ciphertext.Length];
            cipher.Decrypt(iv, ciphertext, authTag, plaintext);
            return plaintext;
        }
    }
}

namespace Keydral.Encryption.Models;

/// <summary>
/// Represents a data encryption key (DEK) used in envelope encryption.
/// The DEK itself is encrypted with the master key.
/// </summary>
public class DataEncryptionKey
{
    /// <summary>
    /// The unencrypted data encryption key bytes.
    /// Must be kept secure in memory and zeros out when disposed.
    /// </summary>
    public byte[] KeyBytes { get; set; }

    /// <summary>
    /// The encrypted data key (encrypted with the master key).
    /// </summary>
    public byte[] EncryptedKeyBytes { get; set; }

    /// <summary>
    /// Initialization vector for the encrypted key.
    /// </summary>
    public byte[] InitializationVector { get; set; }

    /// <summary>
    /// The GCM authentication tag for the encrypted key.
    /// </summary>
    public byte[] AuthenticationTag { get; set; }

    /// <summary>
    /// Unique identifier for this data key.
    /// </summary>
    public Guid KeyId { get; set; }

    /// <summary>
    /// The algorithm used for encryption.
    /// </summary>
    public string Algorithm { get; set; } = "AES-256-GCM";

    /// <summary>
    /// Constructor.
    /// </summary>
    public DataEncryptionKey(
        byte[] keyBytes,
        byte[] encryptedKeyBytes,
        byte[] initializationVector,
        byte[] authenticationTag)
    {
        KeyBytes = keyBytes ?? throw new ArgumentNullException(nameof(keyBytes));
        EncryptedKeyBytes = encryptedKeyBytes ?? throw new ArgumentNullException(nameof(encryptedKeyBytes));
        InitializationVector = initializationVector ?? throw new ArgumentNullException(nameof(initializationVector));
        AuthenticationTag = authenticationTag ?? throw new ArgumentNullException(nameof(authenticationTag));
        KeyId = Guid.NewGuid();
    }

    /// <summary>
    /// Secure cleanup: zero out the unencrypted key bytes.
    /// </summary>
    public void ZeroKeyBytes()
    {
        if (KeyBytes != null && KeyBytes.Length > 0)
        {
            Array.Clear(KeyBytes, 0, KeyBytes.Length);
        }
    }
}

namespace Keydral.Encryption.Models;

/// <summary>
/// Represents an encrypted secret value along with encryption metadata.
/// Used for storage in the database.
/// </summary>
public class EncryptedSecret
{
    /// <summary>
    /// The encrypted secret value.
    /// </summary>
    public string EncryptedData { get; set; }

    /// <summary>
    /// The encrypted data encryption key (encrypted with master key).
    /// </summary>
    public string EncryptedDataKey { get; set; }

    /// <summary>
    /// The initialization vector used for encrypting the data (base64-encoded).
    /// </summary>
    public string InitializationVector { get; set; }

    /// <summary>
    /// The GCM authentication tag for the data (base64-encoded).
    /// </summary>
    public string AuthenticationTag { get; set; }

    /// <summary>
    /// The IV for encrypting the data key (base64-encoded).
    /// </summary>
    public string KeyInitializationVector { get; set; }

    /// <summary>
    /// The GCM authentication tag for the encrypted data key (base64-encoded).
    /// </summary>
    public string KeyAuthenticationTag { get; set; }

    /// <summary>
    /// The ID of the data encryption key used.
    /// </summary>
    public Guid EncryptionKeyId { get; set; }

    /// <summary>
    /// The encryption algorithm used (e.g., "AES-256-GCM").
    /// </summary>
    public string Algorithm { get; set; } = "AES-256-GCM";

    /// <summary>
    /// Constructor.
    /// </summary>
    public EncryptedSecret(
        string encryptedData,
        string encryptedDataKey,
        string initializationVector,
        string authenticationTag,
        string keyInitializationVector,
        string keyAuthenticationTag,
        Guid encryptionKeyId)
    {
        EncryptedData = encryptedData ?? throw new ArgumentNullException(nameof(encryptedData));
        EncryptedDataKey = encryptedDataKey ?? throw new ArgumentNullException(nameof(encryptedDataKey));
        InitializationVector = initializationVector ?? throw new ArgumentNullException(nameof(initializationVector));
        AuthenticationTag = authenticationTag ?? throw new ArgumentNullException(nameof(authenticationTag));
        KeyInitializationVector = keyInitializationVector ?? throw new ArgumentNullException(nameof(keyInitializationVector));
        KeyAuthenticationTag = keyAuthenticationTag ?? throw new ArgumentNullException(nameof(keyAuthenticationTag));
        EncryptionKeyId = encryptionKeyId;
    }
}

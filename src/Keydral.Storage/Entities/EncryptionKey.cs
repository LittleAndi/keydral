namespace Keydral.Storage.Entities;

/// <summary>
/// Represents an encryption key in the envelope encryption model.
/// Stores the encrypted data key (encrypted with the master key).
/// </summary>
public class EncryptionKey
{
    /// <summary>
    /// Unique identifier for this data key.
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Key ID/alias for organizational use (e.g., "primary-2026-q1").
    /// </summary>
    public string KeyId { get; set; } = string.Empty;

    /// <summary>
    /// The data key itself, encrypted with the master key.
    /// Format: Base64-encoded encrypted bytes.
    /// </summary>
    public string EncryptedDataKey { get; set; } = string.Empty;

    /// <summary>
    /// Encryption algorithm used (e.g., "AES-256-GCM").
    /// </summary>
    public string Algorithm { get; set; } = "AES-256-GCM";

    /// <summary>
    /// Initialization vector (IV) for decryption (Base64-encoded).
    /// </summary>
    public string? InitializationVector { get; set; }

    /// <summary>
    /// Flag indicating if this key is currently active for new encryptions.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Timestamp when this encryption key was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Optional timestamp when this key was rotated out.
    /// </summary>
    public DateTime? RotatedAt { get; set; }

    /// <summary>
    /// Navigation property to all secrets encrypted with this key.
    /// </summary>
    public virtual ICollection<Secret> Secrets { get; set; } = new List<Secret>();

    /// <summary>
    /// Navigation property to all secret versions encrypted with this key.
    /// </summary>
    public virtual ICollection<SecretVersion> Versions { get; set; } = new List<SecretVersion>();
}

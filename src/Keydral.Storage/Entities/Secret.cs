namespace Keydral.Storage.Entities;

/// <summary>
/// Represents a secret stored in Keydral.
/// Secrets are immutable once created; updates create new versions.
/// </summary>
public class Secret
{
    /// <summary>
    /// Unique identifier for the secret.
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Human-readable name/path of the secret (e.g., "db-password", "team-a/api-key").
    /// Must be unique.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Encrypted secret value using the envelope encryption model.
    /// </summary>
    public string EncryptedValue { get; set; } = string.Empty;

    /// <summary>
    /// Encrypted data encryption key (encrypted with master key, base64).
    /// </summary>
    public string? EncryptedDataKey { get; set; }

    /// <summary>
    /// Initialization vector for the encrypted data (base64).
    /// </summary>
    public string? InitializationVector { get; set; }

    /// <summary>
    /// GCM authentication tag for the encrypted data (base64).
    /// </summary>
    public string? AuthenticationTag { get; set; }

    /// <summary>
    /// Initialization vector for the encrypted data key (base64).
    /// </summary>
    public string? KeyInitializationVector { get; set; }

    /// <summary>
    /// GCM authentication tag for the encrypted data key (base64).
    /// </summary>
    public string? KeyAuthenticationTag { get; set; }

    /// <summary>
    /// Reference to the data key used to encrypt this secret.
    /// </summary>
    public Guid EncryptionKeyId { get; set; }

    /// <summary>
    /// Navigation property to the encryption key.
    /// </summary>
    public virtual EncryptionKey? EncryptionKey { get; set; }

    /// <summary>
    /// Optional description of the secret.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// User who created this secret.
    /// </summary>
    public string? CreatedBy { get; set; }

    /// <summary>
    /// Tags for categorization (comma-separated).
    /// </summary>
    public string? Tags { get; set; }

    /// <summary>
    /// Optional metadata / tags (JSON).
    /// </summary>
    public string? Metadata { get; set; }

    /// <summary>
    /// Current version number (incremented on each update).
    /// </summary>
    public int CurrentVersion { get; set; } = 1;

    /// <summary>
    /// Timestamp when the secret was first created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Timestamp when the secret was last updated.
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Soft delete flag (for audit trail preservation).
    /// </summary>
    public bool IsDeleted { get; set; } = false;

    /// <summary>
    /// Timestamp when the secret was soft-deleted.
    /// </summary>
    public DateTime? DeletedAt { get; set; }

    /// <summary>
    /// Navigation property to all versions of this secret.
    /// </summary>
    public virtual ICollection<SecretVersion> Versions { get; set; } = new List<SecretVersion>();

    /// <summary>
    /// Navigation property to audit logs for this secret.
    /// </summary>
    public virtual ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();
}

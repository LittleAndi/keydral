namespace Keydral.Storage.Entities;

/// <summary>
/// Represents a version of a secret.
/// Enables retrieving historical secret values without exposing sensitive data.
/// </summary>
public class SecretVersion
{
    /// <summary>
    /// Unique identifier for this version.
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Reference to the parent secret.
    /// </summary>
    public Guid SecretId { get; set; }

    /// <summary>
    /// Navigation property to the parent secret.
    /// </summary>
    public virtual Secret? Secret { get; set; }

    /// <summary>
    /// Version number (incremental, starting at 1).
    /// </summary>
    public int VersionNumber { get; set; }

    /// <summary>
    /// Encrypted secret value for this specific version.
    /// </summary>
    public string EncryptedValue { get; set; } = string.Empty;

    /// <summary>
    /// Reference to the encryption key used for this version.
    /// </summary>
    public Guid EncryptionKeyId { get; set; }

    /// <summary>
    /// Navigation property to the encryption key.
    /// </summary>
    public virtual EncryptionKey? EncryptionKey { get; set; }

    /// <summary>
    /// Timestamp when this version was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Optional change description/reason for this version.
    /// </summary>
    public string? ChangeDescription { get; set; }

    /// <summary>
    /// User who created this version (from Keycloak identity).
    /// </summary>
    public string? CreatedBy { get; set; }
}

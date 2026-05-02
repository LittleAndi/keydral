namespace Keydral.API.Models;

/// <summary>
/// Request DTO for creating or updating a secret.
/// </summary>
public class CreateUpdateSecretRequest
{
    /// <summary>
    /// The secret value.
    /// </summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>
    /// Optional description of the secret.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Tags for categorization (comma-separated).
    /// </summary>
    public string? Tags { get; set; }
}

/// <summary>
/// Response DTO for a secret.
/// </summary>
public class SecretResponse
{
    /// <summary>
    /// Secret identifier.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Secret name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Secret description.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Decrypted secret value.
    /// </summary>
    public string? Value { get; set; }

    /// <summary>
    /// Current version number.
    /// </summary>
    public int Version { get; set; }

    /// <summary>
    /// When the secret was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// When the secret was last updated.
    /// </summary>
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// User who created this secret.
    /// </summary>
    public string? CreatedBy { get; set; }

    /// <summary>
    /// Whether the secret is soft-deleted.
    /// </summary>
    public bool IsDeleted { get; set; }

    /// <summary>
    /// Tags associated with the secret.
    /// </summary>
    public string? Tags { get; set; }
}

/// <summary>
/// Response DTO for secret version history.
/// </summary>
public class SecretVersionResponse
{
    /// <summary>
    /// Version number.
    /// </summary>
    public int VersionNumber { get; set; }

    /// <summary>
    /// Change description.
    /// </summary>
    public string? ChangeDescription { get; set; }

    /// <summary>
    /// When this version was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// User who created this version.
    /// </summary>
    public string? CreatedBy { get; set; }
}

/// <summary>
/// Response DTO for secret list item (without value).
/// </summary>
public class SecretListItemResponse
{
    /// <summary>
    /// Secret identifier.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Secret name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Secret description.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Current version number.
    /// </summary>
    public int Version { get; set; }

    /// <summary>
    /// When the secret was updated.
    /// </summary>
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// Tags associated with the secret.
    /// </summary>
    public string? Tags { get; set; }
}

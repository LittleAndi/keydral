namespace Keydral.Storage.Entities;

/// <summary>
/// Audit log entry tracking all access to secrets.
/// Append-only table for compliance and troubleshooting.
/// </summary>
public class AuditLog
{
    /// <summary>
    /// Unique identifier for this audit log entry.
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// The action performed: "READ", "CREATE", "UPDATE", "DELETE", "FAILED_ACCESS".
    /// </summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>
    /// Resource type being accessed: "SECRET", "POLICY", "ENCRYPTION_KEY".
    /// </summary>
    public string ResourceType { get; set; } = "SECRET";

    /// <summary>
    /// Identifier of the resource being accessed.
    /// </summary>
    public string ResourceId { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable resource name/path (e.g., "db-password").
    /// </summary>
    public string ResourceName { get; set; } = string.Empty;

    /// <summary>
    /// User/principal performing the action (from Keycloak identity).
    /// Format: "user:john", "service:api-client", etc.
    /// </summary>
    public string Actor { get; set; } = string.Empty;

    /// <summary>
    /// IP address or origin of the request.
    /// </summary>
    public string? SourceIp { get; set; }

    /// <summary>
    /// HTTP method/verb used ("GET", "POST", "PUT", "DELETE").
    /// </summary>
    public string? HttpMethod { get; set; }

    /// <summary>
    /// Result of the action: "SUCCESS" or "FAILED".
    /// </summary>
    public string Result { get; set; } = "SUCCESS";

    /// <summary>
    /// HTTP status code if applicable (200, 403, 404, etc.).
    /// </summary>
    public int? StatusCode { get; set; }

    /// <summary>
    /// Error message if the action failed (never includes secret values).
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// User agent of the request (CLI, HTTP client, etc.).
    /// </summary>
    public string? UserAgent { get; set; }

    /// <summary>
    /// Timestamp when the action occurred.
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Request ID for correlation/tracing.
    /// </summary>
    public string? RequestId { get; set; }

    /// <summary>
    /// Reference to the secret if this log is for a secret access (optional FK).
    /// </summary>
    public Guid? SecretId { get; set; }

    /// <summary>
    /// Navigation property to the secret.
    /// </summary>
    public virtual Secret? Secret { get; set; }

    /// <summary>
    /// Additional context/metadata (JSON).
    /// </summary>
    public string? Metadata { get; set; }
}

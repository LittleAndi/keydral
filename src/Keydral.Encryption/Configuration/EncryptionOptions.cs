namespace Keydral.Encryption.Configuration;

/// <summary>
/// Configuration for the encryption service.
/// </summary>
public class EncryptionOptions
{
    /// <summary>
    /// Master key provider type: "file", "kubernetes", or "none" (for testing only).
    /// </summary>
    public string Provider { get; set; } = "file";

    /// <summary>
    /// Path to the master key file (if using file provider).
    /// </summary>
    public string? MasterKeyFilePath { get; set; }

    /// <summary>
    /// Path to the Kubernetes Secret (if using kubernetes provider).
    /// </summary>
    public string? KubernetesSecretPath { get; set; }

    /// <summary>
    /// Encryption algorithm (default: AES-256-GCM).
    /// </summary>
    public string Algorithm { get; set; } = "AES-256-GCM";

    /// <summary>
    /// Enable secure wipe of sensitive data in memory (slower, more secure).
    /// </summary>
    public bool SecureWipeEnabled { get; set; } = true;

    /// <summary>
    /// Validate the algorithm setting only.
    /// This is safe to call eagerly during service registration because it has no I/O dependencies.
    /// </summary>
    public void ValidateAlgorithm()
    {
        if (Algorithm != "AES-256-GCM")
        {
            throw new InvalidOperationException(
                $"Unsupported algorithm: '{Algorithm}'. " +
                "The only supported algorithm is 'AES-256-GCM'. " +
                "Set Encryption:Algorithm to 'AES-256-GCM' or remove it to use the default.");
        }
    }

    /// <summary>
    /// Validate configuration.
    /// </summary>
    public void Validate()
    {
        ValidateAlgorithm();

        switch (Provider.ToLowerInvariant())
        {
            case "file":
                if (string.IsNullOrWhiteSpace(MasterKeyFilePath))
                    throw new InvalidOperationException(
                        "MasterKeyFilePath is required when Provider is 'file'");
                break;

            case "kubernetes":
                // KubernetesSecretPath has a default, but can be customized
                break;

            case "none":
                // For testing only
                break;

            default:
                throw new InvalidOperationException(
                    $"Unknown encryption provider: {Provider}. " +
                    "Use 'file', 'kubernetes', or 'none'");
        }
    }
}

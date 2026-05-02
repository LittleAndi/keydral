namespace Keydral.Encryption.Providers;

/// <summary>
/// Kubernetes Secret-based master key provider.
/// Reads the master key from a Kubernetes Secret mounted as a volume.
/// </summary>
/// <remarks>
/// This is a stub for MVP. Full implementation requires:
/// - Reading from /var/run/secrets/kubernetes.io/serviceaccount/ for token
/// - Kubernetes API client (e.g., KubernetesClient)
/// - Mounting the master key Secret via volumeMounts
/// </remarks>
public class KubernetesSecretMasterKeyProvider : IMasterKeyProvider
{
    private readonly string _secretKeyPath;
    private byte[]? _cachedKey;
    private const int MasterKeySize = 32; // 256-bit key

    public string Algorithm => "AES-256-GCM";
    public int KeySizeInBits => MasterKeySize * 8;

    /// <summary>
    /// Constructor.
    /// </summary>
    /// <param name="secretKeyPath">Path to the master key file in the K8s Secret mount (default: /var/run/secrets/keydral/master-key)</param>
    public KubernetesSecretMasterKeyProvider(string secretKeyPath = "/var/run/secrets/keydral/master-key")
    {
        if (string.IsNullOrWhiteSpace(secretKeyPath))
            throw new ArgumentException("Secret key path cannot be empty", nameof(secretKeyPath));

        _secretKeyPath = secretKeyPath;
    }

    /// <summary>
    /// Get the master key from the Kubernetes Secret.
    /// </summary>
    public async Task<byte[]> GetMasterKeyAsync(CancellationToken cancellationToken = default)
    {
        if (_cachedKey != null && _cachedKey.Length == MasterKeySize)
        {
            return _cachedKey;
        }

        if (!File.Exists(_secretKeyPath))
        {
            throw new InvalidOperationException(
                $"Kubernetes Secret key file not found at: {_secretKeyPath}. " +
                "Ensure the Secret is mounted as a volume with the key 'master-key'.");
        }

        try
        {
            var keyContent = await File.ReadAllTextAsync(_secretKeyPath, cancellationToken);
            keyContent = keyContent.Trim();

            byte[] keyBytes;
            if (keyContent.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                keyBytes = Convert.FromHexString(keyContent[2..]);
            }
            else
            {
                keyBytes = Convert.FromBase64String(keyContent);
            }

            if (keyBytes.Length != MasterKeySize)
            {
                throw new InvalidOperationException(
                    $"Master key must be 256 bits, but got {keyBytes.Length * 8} bits");
            }

            _cachedKey = keyBytes;
            return keyBytes;
        }
        catch (Exception ex) when (!(ex is InvalidOperationException))
        {
            throw new InvalidOperationException(
                $"Failed to read master key from Kubernetes Secret: {_secretKeyPath}", ex);
        }
    }

    /// <summary>
    /// Get the master key identifier from the Secret name.
    /// </summary>
    public Task<string> GetMasterKeyIdAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult("k8s-secret");
    }

    /// <summary>
    /// Check if the Kubernetes Secret is accessible.
    /// </summary>
    public Task<bool> IsMasterKeyAvailableAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return Task.FromResult(File.Exists(_secretKeyPath));
        }
        catch
        {
            return Task.FromResult(false);
        }
    }

    /// <summary>
    /// Securely clear the cached key from memory.
    /// </summary>
    public void ClearCache()
    {
        if (_cachedKey != null)
        {
            Array.Clear(_cachedKey, 0, _cachedKey.Length);
            _cachedKey = null;
        }
    }
}

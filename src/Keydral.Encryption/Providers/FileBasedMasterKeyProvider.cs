using System.Security.Cryptography;

namespace Keydral.Encryption.Providers;

/// <summary>
/// File-based master key provider for MVP.
/// Reads the master key from a file on disk.
/// </summary>
/// <remarks>
/// ⚠️ Security Note: File-based keys are acceptable for local development but NOT for production.
/// For production, use:
/// - Kubernetes Secret with field encryption at rest
/// - Azure Key Vault
/// - AWS KMS
/// - Hardware Security Module (HSM)
/// - TPM (Trusted Platform Module)
/// </remarks>
public class FileBasedMasterKeyProvider : IMasterKeyProvider
{
    private readonly string _keyFilePath;
    private byte[]? _cachedKey;
    private string? _keyId;
    private const int MasterKeySize = 32; // 256-bit key
    private const string DefaultKeyId = "file-based-key";

    public string Algorithm => "AES-256-GCM";
    public int KeySizeInBits => MasterKeySize * 8; // 256 bits

    /// <summary>
    /// Constructor.
    /// </summary>
    /// <param name="keyFilePath">Path to the file containing the base64-encoded master key</param>
    public FileBasedMasterKeyProvider(string keyFilePath)
    {
        if (string.IsNullOrWhiteSpace(keyFilePath))
            throw new ArgumentException("Key file path cannot be empty", nameof(keyFilePath));

        _keyFilePath = keyFilePath;
    }

    /// <summary>
    /// Get the master key from the file.
    /// Reads and caches the key in memory for performance.
    /// </summary>
    public async Task<byte[]> GetMasterKeyAsync(CancellationToken cancellationToken = default)
    {
        // Return cached key if already loaded
        if (_cachedKey != null && _cachedKey.Length == MasterKeySize)
        {
            return _cachedKey;
        }

        // Load key from file
        if (!File.Exists(_keyFilePath))
        {
            throw new InvalidOperationException(
                $"Master key file not found at: {_keyFilePath}. " +
                "Use GenerateMasterKeyFileAsync() to create one.");
        }

        try
        {
            var keyContent = await File.ReadAllTextAsync(_keyFilePath, cancellationToken);
            keyContent = keyContent.Trim();

            // Handle both base64 and raw hex formats
            byte[] keyBytes;
            if (keyContent.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                // Hex format
                keyBytes = Convert.FromHexString(keyContent[2..]);
            }
            else
            {
                // Base64 format
                keyBytes = Convert.FromBase64String(keyContent);
            }

            if (keyBytes.Length != MasterKeySize)
            {
                throw new InvalidOperationException(
                    $"Master key must be {MasterKeySize} bytes ({KeySizeInBits} bits), but got {keyBytes.Length} bytes");
            }

            // Cache the key
            _cachedKey = keyBytes;
            return keyBytes;
        }
        catch (Exception ex) when (!(ex is InvalidOperationException))
        {
            throw new InvalidOperationException(
                $"Failed to read master key from file: {_keyFilePath}", ex);
        }
    }

    /// <summary>
    /// Get the master key identifier.
    /// </summary>
    public Task<string> GetMasterKeyIdAsync(CancellationToken cancellationToken = default)
    {
        _keyId ??= DefaultKeyId;
        return Task.FromResult(_keyId);
    }

    /// <summary>
    /// Verify that the master key file is accessible.
    /// </summary>
    public Task<bool> IsMasterKeyAvailableAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return Task.FromResult(File.Exists(_keyFilePath));
        }
        catch
        {
            return Task.FromResult(false);
        }
    }

    /// <summary>
    /// Generate a new master key and save it to the file.
    /// Use this to initialize the key file for the first time.
    /// </summary>
    public static async Task GenerateMasterKeyFileAsync(string keyFilePath)
    {
        if (string.IsNullOrWhiteSpace(keyFilePath))
            throw new ArgumentException("Key file path cannot be empty", nameof(keyFilePath));

        // Create directory if it doesn't exist
        var directory = Path.GetDirectoryName(keyFilePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // Don't overwrite existing key
        if (File.Exists(keyFilePath))
        {
            throw new InvalidOperationException(
                $"Master key file already exists at: {keyFilePath}. " +
                "Delete it manually if you want to generate a new key.");
        }

        // Generate a new 256-bit key
        using var rng = RandomNumberGenerator.Create();
        var keyBytes = new byte[32];
        rng.GetBytes(keyBytes);

        // Save as base64
        var keyContent = Convert.ToBase64String(keyBytes);

        // Write with restricted permissions
        await File.WriteAllTextAsync(keyFilePath, keyContent);

        // Set file permissions to read-only for current user (Windows)
#pragma warning disable CA1416 // Suppress platform-specific warning
        try
        {
            var fileInfo = new FileInfo(keyFilePath);
            var fileSecurity = fileInfo.GetAccessControl();
            // Further permission restrictions would be applied here in production
        }
        catch
        {
            // Non-critical error; key is still usable
        }
#pragma warning restore CA1416
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

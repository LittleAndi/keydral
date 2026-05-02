namespace Keydral.Encryption.Providers;

/// <summary>
/// Interface for master key providers.
/// Master keys are used to encrypt/decrypt data encryption keys (envelope encryption).
/// </summary>
public interface IMasterKeyProvider
{
    /// <summary>
    /// Get the master key bytes (must be stored securely in memory).
    /// </summary>
    /// <remarks>
    /// Returns a byte array that must be securely managed (wiped from memory after use).
    /// </remarks>
    Task<byte[]> GetMasterKeyAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get the master key ID/identifier.
    /// </summary>
    Task<string> GetMasterKeyIdAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Verify that a master key is available and accessible.
    /// </summary>
    Task<bool> IsMasterKeyAvailableAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get the key algorithm/cipher (e.g., "AES-256-GCM").
    /// </summary>
    string Algorithm { get; }

    /// <summary>
    /// Get the key size in bits.
    /// </summary>
    int KeySizeInBits { get; }
}

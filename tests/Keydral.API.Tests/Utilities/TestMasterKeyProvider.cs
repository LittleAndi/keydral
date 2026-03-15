using Keydral.Encryption.Providers;

namespace Keydral.API.Tests.Utilities;

/// <summary>
/// Test master key provider using a fixed, deterministic test key.
/// Safe to use in tests - the key is not a production secret.
/// </summary>
internal class TestMasterKeyProvider : IMasterKeyProvider
{
    private readonly byte[] _masterKey;

    public string Algorithm => "AES-256-GCM";
    public int KeySizeInBits => 256;

    public TestMasterKeyProvider()
    {
        // Fixed test key (256 bits / 32 bytes) - NOT FOR PRODUCTION
        _masterKey = new byte[]
        {
            0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07,
            0x08, 0x09, 0x0A, 0x0B, 0x0C, 0x0D, 0x0E, 0x0F,
            0x10, 0x11, 0x12, 0x13, 0x14, 0x15, 0x16, 0x17,
            0x18, 0x19, 0x1A, 0x1B, 0x1C, 0x1D, 0x1E, 0x1F
        };
    }

    public Task<byte[]> GetMasterKeyAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_masterKey);
    }

    public Task<string> GetMasterKeyIdAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult("test-key");
    }

    public Task<bool> IsMasterKeyAvailableAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(true);
    }
}

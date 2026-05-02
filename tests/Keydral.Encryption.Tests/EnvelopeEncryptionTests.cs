using Keydral.Encryption.Models;
using Keydral.Encryption.Configuration;
using Keydral.Encryption.Providers;

namespace Keydral.Encryption.Tests;

/// <summary>
/// Tests for envelope encryption service.
/// </summary>
public class EnvelopeEncryptionTests
{
    private readonly IMasterKeyProvider _masterKeyProvider;
    private readonly IEncryptionService _encryptionService;

    public EnvelopeEncryptionTests()
    {
        // Create a test master key provider with a fixed key
        _masterKeyProvider = new TestMasterKeyProvider();
        _encryptionService = new EnvelopeEncryptionService(_masterKeyProvider, new EncryptionOptions());
    }

    [Fact]
    public async Task Encrypt_ShouldEncryptSecretValue()
    {
        // Arrange
        var plaintext = "my-secret-password-123";

        // Act
        var encrypted = await _encryptionService.EncryptAsync(plaintext);

        // Assert
        Assert.NotNull(encrypted);
        Assert.NotEmpty(encrypted.EncryptedData);
        Assert.NotEqual(plaintext, encrypted.EncryptedData);
    }

    [Fact]
    public async Task Decrypt_ShouldDecryptEncryptedSecret()
    {
        // Arrange
        var plaintext = "my-secret-password-123";
        var encrypted = await _encryptionService.EncryptAsync(plaintext);

        // Act
        var decrypted = await _encryptionService.DecryptAsync(encrypted);

        // Assert
        Assert.Equal(plaintext, decrypted);
    }

    [Fact]
    public async Task EncryptDecrypt_RoundTrip_WithMultipleSecrets()
    {
        // Arrange
        var secrets = new[]
        {
            "password123",
            "api-key-with-special-chars-!@#$%",
            "🔐 emoji password 🔑",
            "s",  // Single character
            "very long secret " + new string('x', 1000)
        };

        // Act & Assert
        foreach (var secret in secrets)
        {
            var encrypted = await _encryptionService.EncryptAsync(secret);
            var decrypted = await _encryptionService.DecryptAsync(encrypted);
            Assert.Equal(secret, decrypted);
        }
    }

    [Fact]
    public async Task GenerateDataEncryptionKey_ShouldCreateValidKey()
    {
        // Act
        var dek = await _encryptionService.GenerateDataEncryptionKeyAsync();

        // Assert
        Assert.NotNull(dek);
        Assert.NotEmpty(dek.KeyBytes);
        Assert.NotEmpty(dek.EncryptedKeyBytes);
        Assert.NotEmpty(dek.InitializationVector);
        Assert.NotEmpty(dek.AuthenticationTag);
        Assert.NotEqual(Guid.Empty, dek.KeyId);
    }

    [Fact]
    public async Task DecryptDataEncryptionKey_ShouldRecoverOriginalKey()
    {
        // Arrange
        var originalDek = await _encryptionService.GenerateDataEncryptionKeyAsync();
        var encryptedKeyBytes = originalDek.EncryptedKeyBytes;
        var iv = originalDek.InitializationVector;
        var authTag = originalDek.AuthenticationTag;

        // Act
        var decryptedDek = await _encryptionService.DecryptDataEncryptionKeyAsync(encryptedKeyBytes, iv, authTag);

        // Assert
        Assert.NotNull(decryptedDek);
        Assert.Equal(originalDek.KeyBytes, decryptedDek.KeyBytes);
    }

    [Fact]
    public async Task DifferentCalls_ShouldProduceDifferentCiphertexts()
    {
        // Arrange
        var plaintext = "same-secret";

        // Act
        var encrypted1 = await _encryptionService.EncryptAsync(plaintext);
        var key1 = encrypted1.EncryptedDataKey;
        var iv1 = encrypted1.KeyInitializationVector;
        var tag1 = encrypted1.KeyAuthenticationTag;

        var encrypted2 = await _encryptionService.EncryptAsync(plaintext);
        var key2 = encrypted2.EncryptedDataKey;
        var iv2 = encrypted2.KeyInitializationVector;
        var tag2 = encrypted2.KeyAuthenticationTag;

        // Assert different ciphertexts
        Assert.NotEqual(encrypted1.EncryptedData, encrypted2.EncryptedData);

        // Assert different DEKs (each call generates a new DEK)
        Assert.NotEqual(key1, key2);
        Assert.NotEqual(iv1, iv2);
        Assert.NotEqual(tag1, tag2);

        // But both should decrypt to the same plaintext
        var decrypted1 = await _encryptionService.DecryptAsync(encrypted1);
        Assert.Equal(plaintext, decrypted1);

        var decrypted2 = await _encryptionService.DecryptAsync(encrypted2);
        Assert.Equal(plaintext, decrypted2);
    }

    [Fact]
    public async Task EncryptAsyncWithNullOrEmptySecret_ShouldThrow()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => _encryptionService.EncryptAsync(null!));
        await Assert.ThrowsAsync<ArgumentException>(() => _encryptionService.EncryptAsync(""));
    }

    [Fact]
    public async Task DecryptAsyncWithNullSecret_ShouldThrow()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => _encryptionService.DecryptAsync(null!));
    }

    [Fact]
    public void Constructor_WithUnsupportedAlgorithm_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var options = new EncryptionOptions { Algorithm = "AES-128-GCM" };

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(
            () => new EnvelopeEncryptionService(_masterKeyProvider, options));
        Assert.Contains("AES-128-GCM", ex.Message);
        Assert.Contains("AES-256-GCM", ex.Message);
    }

    [Fact]
    public async Task EncryptDecrypt_WithSecureWipeDisabled_ShouldRoundTrip()
    {
        // Arrange
        var options = new EncryptionOptions { SecureWipeEnabled = false };
        var service = new EnvelopeEncryptionService(_masterKeyProvider, options);
        var plaintext = "secret-no-secure-wipe";

        // Act
        var encrypted = await service.EncryptAsync(plaintext);
        var decrypted = await service.DecryptAsync(encrypted);

        // Assert
        Assert.Equal(plaintext, decrypted);
    }
}

/// <summary>
/// Test implementation of master key provider.
/// Uses a fixed key for deterministic testing.
/// </summary>
public class TestMasterKeyProvider : IMasterKeyProvider
{
    private readonly byte[] _masterKey;

    public string Algorithm => "AES-256-GCM";
    public int KeySizeInBits => 256;

    public TestMasterKeyProvider()
    {
        // Fixed test key (256 bits / 32 bytes) - DO NOT USE IN PRODUCTION
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

using System.Runtime.InteropServices;

namespace Keydral.Encryption.Utilities;

/// <summary>
/// Utilities for secure memory management.
/// Provides methods to securely wipe sensitive data from memory.
/// </summary>
public static class SecureMemory
{
    /// <summary>
    /// Securely zeros out a byte array to prevent data leakage.
    /// </summary>
    /// <remarks>
    /// Uses <see cref="System.Security.Cryptography.CryptographicOperations.ZeroMemory"/>
    /// which is guaranteed not to be elided by the JIT compiler.
    /// </remarks>
    public static void ZeroBytes(byte[] data)
    {
        if (data == null || data.Length == 0)
            return;

        System.Security.Cryptography.CryptographicOperations.ZeroMemory(data);
    }

    /// <summary>
    /// Securely zeros out a char array (for strings, SecureString is preferred).
    /// </summary>
    public static void ZeroChars(char[] data)
    {
        if (data == null || data.Length == 0)
            return;

        System.Security.Cryptography.CryptographicOperations.ZeroMemory(
            System.Runtime.InteropServices.MemoryMarshal.AsBytes(data.AsSpan()));
    }

    /// <summary>
    /// Securely wipe sensitive bytes with random data before clearing.
    /// More secure than simple clearing, but slower.
    /// </summary>
    public static void SecureWipeBytes(byte[] data)
    {
        if (data == null || data.Length == 0)
            return;

        // First pass: write random data
        using (var rng = System.Security.Cryptography.RandomNumberGenerator.Create())
        {
            rng.GetBytes(data);
        }

        // Second pass: write zeros
        System.Security.Cryptography.CryptographicOperations.ZeroMemory(data);
    }

    /// <summary>
    /// Create a disposable wrapper that auto-clears data when disposed.
    /// </summary>
    public class SecureBuffer : IDisposable
    {
        private byte[]? _data;
        private readonly GCHandle _pinnedHandle;

        public SecureBuffer(int size)
        {
            _data = new byte[size];
            _pinnedHandle = GCHandle.Alloc(_data, GCHandleType.Pinned);
        }

        public byte[] Data => _data ?? throw new ObjectDisposedException(nameof(SecureBuffer));

        public void Dispose()
        {
            if (_data != null)
            {
                ZeroBytes(_data);
            }

            if (_pinnedHandle.IsAllocated)
            {
                _pinnedHandle.Free();
            }

            _data = null;
            GC.SuppressFinalize(this);
        }

        ~SecureBuffer()
        {
            Dispose();
        }
    }
}

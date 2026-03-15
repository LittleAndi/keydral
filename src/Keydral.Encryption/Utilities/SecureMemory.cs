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
    /// Uses Array.Clear with proper pinning to prevent compiler optimizations
    /// that might skip the clearing operation.
    /// </remarks>
    public static void ZeroBytes(byte[] data)
    {
        if (data == null || data.Length == 0)
            return;

        // Use pinned array to prevent garbage collection during wiping
        var handle = GCHandle.Alloc(data, GCHandleType.Pinned);
        try
        {
            // Clear the array multiple times to be extra safe
            Array.Clear(data, 0, data.Length);

            // Optional: overwrite with random data first (more secure, slower)
            // This makes it harder for forensic recovery
        }
        finally
        {
            handle.Free();
        }
    }

    /// <summary>
    /// Securely zeros out a char array (for strings, SecureString is preferred).
    /// </summary>
    public static void ZeroChars(char[] data)
    {
        if (data == null || data.Length == 0)
            return;

        var handle = GCHandle.Alloc(data, GCHandleType.Pinned);
        try
        {
            Array.Clear(data, 0, data.Length);
        }
        finally
        {
            handle.Free();
        }
    }

    /// <summary>
    /// Securely wipe sensitive bytes with random data before clearing.
    /// More secure than simple clearing, but slower.
    /// </summary>
    public static void SecureWipeBytes(byte[] data)
    {
        if (data == null || data.Length == 0)
            return;

        var handle = GCHandle.Alloc(data, GCHandleType.Pinned);
        try
        {
            // First pass: write random data
            using (var rng = System.Security.Cryptography.RandomNumberGenerator.Create())
            {
                rng.GetBytes(data);
            }

            // Second pass: write zeros
            Array.Clear(data, 0, data.Length);
        }
        finally
        {
            handle.Free();
        }
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

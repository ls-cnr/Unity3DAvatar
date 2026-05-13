// ============================================================================
// PinnedBuffer.cs
// ============================================================================
// Provides a SAFE alternative to C# 'unsafe' / 'fixed' statements for
// long-lived memory pinning. Used by FaceDetection and HeadPoseEstimation
// to give OpenCV stable memory addresses for zero-copy tensor preparation.
//
// DEPENDENCIES: None (standalone utility)
// USED BY: FaceDetection.cs, HeadPoseEstimation.cs
// ============================================================================

using System;
using System.Runtime.InteropServices;

/// <summary>
/// Wraps a managed array with a pinned GCHandle for stable pointer access.
/// 
/// PROBLEM SOLVED:
///   OpenCV's Mat.FromPixelData() requires a stable IntPtr to memory.
///   C# arrays can move during garbage collection. The 'fixed' keyword pins
///   memory temporarily, but cannot span multiple method calls or frames.
///   
/// SOLUTION:
///   GCHandle.Alloc(..., Pinned) pins the array for its entire lifetime.
///   The array never moves. We expose the pointer via IntPtr for OpenCV.
///   
/// LIFECYCLE:
///   1. Create PinnedBuffer (array is pinned immediately)
///   2. Pass Pointer / ElementOffset to OpenCV Mat wrappers
///   3. Call Dispose() when done (unpins memory)
///   
/// IMPORTANT: No finalizer. You MUST call Dispose(). The existing code calls
/// it in OnDestroy() via the IDisposable pattern.
/// </summary>
public sealed class PinnedBuffer<T> : IDisposable where T : unmanaged
{
    // -------------------------------------------------------------------------
    // FIELDS
    // -------------------------------------------------------------------------

    /// <summary>The managed array being pinned.</summary>
    private readonly T[] _array;

    /// <summary>The GCHandle that prevents GC from moving the array.</summary>
    private GCHandle _handle;

    /// <summary>Prevents double-dispose.</summary>
    private bool _disposed;

    // -------------------------------------------------------------------------
    // PROPERTIES
    // -------------------------------------------------------------------------

    /// <summary>Access the underlying managed array (safe to read/write).</summary>
    public T[] Array => _array;

    /// <summary>The stable IntPtr to the start of the pinned array.</summary>
    public IntPtr Pointer => _handle.AddrOfPinnedObject();

    /// <summary>True if the GCHandle is still allocated.</summary>
    public bool IsAllocated => _handle.IsAllocated;

    // -------------------------------------------------------------------------
    // CONSTRUCTORS
    // -------------------------------------------------------------------------

    /// <summary>
    /// Creates a new pinned array of the specified length.
    /// The array is immediately pinned and cannot move until Dispose().
    /// </summary>
    /// <param name="length">Number of elements in the array.</param>
    public PinnedBuffer(int length)
    {
        if (length <= 0) 
            throw new ArgumentException("Length must be positive", nameof(length));

        _array = new T[length];
        _handle = GCHandle.Alloc(_array, GCHandleType.Pinned);
    }

    /// <summary>
    /// Pins an EXISTING array. The array must not be pinned elsewhere.
    /// </summary>
    /// <param name="existing">The array to pin.</param>
    public PinnedBuffer(T[] existing)
    {
        _array = existing ?? throw new ArgumentNullException(nameof(existing));
        _handle = GCHandle.Alloc(_array, GCHandleType.Pinned);
    }

    // -------------------------------------------------------------------------
    // PUBLIC METHODS
    // -------------------------------------------------------------------------

    /// <summary>
    /// Gets a pointer offset by element count, equivalent to 'ptr + offset' in C.
    /// 
    /// EXAMPLE:
    ///   For a float[3*480*640] buffer representing an RGB image:
    ///   - ElementOffset(0)          -> Red channel start
    ///   - ElementOffset(640*480)    -> Green channel start  
    ///   - ElementOffset(640*480*2)  -> Blue channel start
    /// </summary>
    /// <param name="elementOffset">Number of elements to offset (not bytes).</param>
    public IntPtr ElementOffset(int elementOffset)
    {
        if (elementOffset < 0 || elementOffset >= _array.Length)
            throw new ArgumentOutOfRangeException(nameof(elementOffset));

        // IntPtr.Add takes bytes, so multiply by element size
        return IntPtr.Add(_handle.AddrOfPinnedObject(), elementOffset * Marshal.SizeOf<T>());
    }

    /// <summary>
    /// Releases the GCHandle, allowing the GC to move/collect the array.
    /// Call this in your class's Dispose() method.
    /// </summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            if (_handle.IsAllocated)
                _handle.Free();
            _disposed = true;
        }
    }
}
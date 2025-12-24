using System;
using System.Runtime.InteropServices;

namespace PlcCommunication.Helpers
{
    /// <summary>
    /// Represents an array that is pinned in memory, 
    /// preventing the garbage collector from moving its memory location.
    /// </summary>
    /// <typeparam name="T">The type of the elements in the array must be an unmanaged type.</typeparam>
    public class PinnedArray<T> : IDisposable where T : unmanaged
    {
        private GCHandle _handle;
        private bool _disposed;
        
        /// <summary>
        /// Initializes a new instance of the PinnedArray class.
        /// </summary>
        /// <param name="array">The array to pin in memory.</param>
        public PinnedArray(T[] array)
        {
            _handle = GCHandle.Alloc(array, GCHandleType.Pinned);
        }
        
        /// <summary>
        /// Gets the memory address of the pinned array.
        /// </summary>
        /// <returns>The memory address of the pinned array.</returns>
        public IntPtr AddrOfPinnedObject()
        {
            return _handle.AddrOfPinnedObject();
        }
        
        /// <summary>
        /// Disposes of the PinnedArray object.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        
        /// <summary>
        /// Frees the memory used by the pinned array.
        /// </summary>
        /// <param name="disposing"></param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // Dispose managed resources.
                }

                if (_handle.IsAllocated)
                {
                    _handle.Free();
                }

                _disposed = true;
            }
        }
        
        /// <summary>
        /// Destructor, called during garbage collection, releases unmanaged resources.
        /// </summary>
        ~PinnedArray()
        {
            Dispose(false);
        }
    }
}
using System;
using System.Runtime.InteropServices;

namespace PlcCommunication.Helpers
{
    /// <summary>
    /// Represents a wrapper for an unmanaged memory pointer, 
    /// responsible for releasing the unmanaged memory and preventing memory leaks.
    /// </summary>
    public class UnmanagedPointer : IDisposable
    {
        /// <summary>
        /// Gets the unmanaged memory pointer.
        /// </summary>
        public IntPtr Pointer { get; private set; }
        
        /// <summary>
        /// Used to determine if the object has been disposed.
        /// </summary>
        private bool disposed = false;

        /// <summary>
        /// Init UnmanagedPointer Class.
        /// </summary>
        /// <param name="pointer">The unmanaged memory pointer.</param>
        public UnmanagedPointer(IntPtr pointer)
        {
            Pointer = pointer;
        }

        /// <summary>
        /// Frees by UnmanagedPointer all resources occupied.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this); // Preventing the garbage collector from calling destructors
        }
        
        /// <summary>
        /// Releases unmanaged resources. Optionally, releases managed resources.
        /// </summary>
        /// <param name="disposing">True to release both managed and unmanaged resources; false to release only unmanaged resources.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!disposed)
            {
                if (Pointer != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(Pointer);
                    Pointer = IntPtr.Zero; // Prevent re-release
                }
                disposed = true; // Mark as released
            }
        }
        
        /// <summary>
        /// Destructor for the UnmanagedPointer class.
        /// </summary>
        ~UnmanagedPointer()
        {
            Dispose(false);
        }
    }
}
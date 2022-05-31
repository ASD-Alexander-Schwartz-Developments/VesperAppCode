namespace ASDLibUSBWrapper.Descriptors
{
    internal abstract class MemoryBlock
    {
        private readonly int mMaxSize;

        private IntPtr mMemPointer = IntPtr.Zero;

        protected MemoryBlock(int maxSize)
        {
            this.mMaxSize = maxSize;
            this.mMemPointer = Marshal.AllocHGlobal(maxSize);
        }

        public int MaxSize
        {
            get { return this.mMaxSize; }
        }

        public IntPtr Ptr
        {
            get { return this.mMemPointer; }
        }

        public void Free()
        {
            if (this.mMemPointer != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(this.mMemPointer);
                this.mMemPointer = IntPtr.Zero;
            }
        }

        ~MemoryBlock()
        {
            this.Free();
        }
    }
}

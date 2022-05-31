
namespace ASDLibUSBWrapper.Descriptors
{
    internal class LangStringDescriptor : MemoryBlock
    {
        #region FIELD_OFFSETS

        private static readonly int OfsDescriptorType = Marshal.OffsetOf(typeof(UsbDescriptor), "DescriptorType").ToInt32();
        private static readonly int OfsLength = Marshal.OffsetOf(typeof(UsbDescriptor), "Length").ToInt32();

        #endregion

        public LangStringDescriptor(int maxSize)
            : base(maxSize)
        {
        }

        public DescriptorType DescriptorType
        {
            get { return (DescriptorType)Marshal.ReadByte(this.Ptr, OfsDescriptorType); }
            set { Marshal.WriteByte(this.Ptr, OfsDescriptorType, (byte)value); }
        }

        public byte Length
        {
            get { return Marshal.ReadByte(this.Ptr, OfsLength); }
            set { Marshal.WriteByte(this.Ptr, OfsLength, value); }
        }

        public bool Get(out short[] langIds)
        {
            langIds = new short[0];
            int totalLength = this.Length;
            if (totalLength <= 2)
            {
                return false;
            }

            int elementCount = (totalLength - 2) / 2;
            langIds = new short[elementCount];

            int startOffset = UsbDescriptor.Size;
            for (int iElement = 0; iElement < langIds.Length; iElement++)
            {
                langIds[iElement] = Marshal.ReadInt16(this.Ptr, startOffset + (sizeof(ushort) * iElement));
            }

            return true;
        }

        public bool Get(out byte[] bytes)
        {
            bytes = new byte[this.Length];
            Marshal.Copy(this.Ptr, bytes, 0, bytes.Length);
            return true;
        }

        public bool Get(out string str)
        {
            str = string.Empty;

            byte[] bytes;
            if (this.Get(out bytes))
            {
                if (bytes.Length <= UsbDescriptor.Size)
                {
                    str = string.Empty;
                }
                else
                {
                    str = Encoding.Unicode.GetString(bytes, UsbDescriptor.Size, bytes.Length - UsbDescriptor.Size);
                }

                return true;
            }

            return false;
        }

    }
}

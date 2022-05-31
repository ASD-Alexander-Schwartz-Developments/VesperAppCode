
#pragma warning disable 649

namespace ASDLibUSBWrapper.Descriptors
{
    [StructLayout(LayoutKind.Sequential)]
    public abstract class UsbDescriptor
    {
        /// <summary>
        /// String value used to seperate the name/value pairs for all ToString overloads of the descriptor classes.
        /// </summary>
        public const string ToStringParamValueSeperator = ":";

        /// <summary>
        /// String value used to seperate the name/value groups for all ToString overloads of the descriptor classes.
        /// </summary>
        public const string ToStringFieldSeperator = "\r\n";

        /// <summary>
        /// Total size of this structure in bytes.
        /// </summary>
        public static readonly int Size = Marshal.SizeOf(typeof(UsbDescriptor));

        /// <summary>
        /// Length of structure reported by the associated usb device.
        /// </summary>
        private byte length;

        /// <summary>
        /// Type of structure reported by the associated usb device.
        /// </summary>
        private DescriptorType descriptorType;

        /// <inheritdoc/>
        public override string ToString()
        {
            object[] values = { this.length, this.descriptorType };
            string[] names = { "Length", "DescriptorType" };

            return Helper.ToString(string.Empty, names, ToStringParamValueSeperator, values, ToStringFieldSeperator);
        }

    }
}

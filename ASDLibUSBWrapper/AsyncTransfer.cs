using System.Collections.Concurrent;

namespace ASDLibUSBWrapper
{
    public class AsyncTransfer
    {
        private static readonly ConcurrentDictionary<int, ManualResetEventSlim> Transfers = new ConcurrentDictionary<int, ManualResetEventSlim>();
        private static readonly object TransferLock = new object();
        private static int transferIndex = 0;

        private static unsafe TransferDelegate transferDelegate = new TransferDelegate(Callback);
        private static IntPtr transferDelegatePtr = Marshal.GetFunctionPointerForDelegate(transferDelegate);

        public static LibUsbError TransferAsync(
            LibUsbDeviceHandle device,
            byte endPoint,
            EndpointType endPointType,
            IntPtr buffer,
            int offset,
            int length,
            int timeout,
            out int transferLength)
        {
            return TransferAsync(device, endPoint, endPointType, buffer, offset, length, timeout, 0, out transferLength);
        }

        internal static unsafe LibUsbError TransferAsync(
            LibUsbDeviceHandle device,
            byte endPoint,
            EndpointType endPointType,
            IntPtr buffer,
            int offset,
            int length,
            int timeout,
            int isoPacketSize,
            out int transferLength)
        {
            if (device == null)
            {
                throw new ArgumentNullException(nameof(device));
            }

            if (offset < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(offset));
            }

            // Determin the amount of isosynchronous packets
            int numIsoPackets = 0;

            if (isoPacketSize > 0)
            {
                numIsoPackets = length / isoPacketSize;
            }

            var transfer = NativeImport.AllocTransfer(numIsoPackets);

            ManualResetEventSlim mre = new ManualResetEventSlim(false);

            int transferId = 0;

            lock (TransferLock)
            {
                transferId = transferIndex++;
            }

            Transfers.AddOrUpdate(transferId, mre, (index, data) => throw new NotImplementedException());

            // Fill common properties
            transfer->DevHandle = device.DangerousGetHandle();
            transfer->Endpoint = endPoint;
            transfer->Timeout = (uint)timeout;
            transfer->Type = (byte)endPointType;
            transfer->Buffer = (byte*)buffer + offset;
            transfer->Length = length;
            transfer->NumIsoPackets = numIsoPackets;
            transfer->Flags = (byte)TransferFlags.None;
            transfer->Callback = transferDelegatePtr;
            transfer->UserData = new IntPtr(transferId);

            NativeImport.SubmitTransfer(transfer).ThrowOnError();

            transferLength = 0;
            mre.Wait();

            transferLength = transfer->ActualLength;

            LibUsbError ret = LibUsbError.Success;
            switch (transfer->Status)
            {
                case TransferStatus.Completed:
                    ret = LibUsbError.Success;
                    break;

                case TransferStatus.TimedOut:
                    ret = LibUsbError.Timeout;
                    break;

                case TransferStatus.Stall:
                    ret = LibUsbError.Pipe;
                    break;

                case TransferStatus.Overflow:
                    ret = LibUsbError.Overflow;
                    break;

                case TransferStatus.NoDevice:
                    ret = LibUsbError.NoDevice;
                    break;

                case TransferStatus.Error:
                case TransferStatus.Cancelled:
                    ret = LibUsbError.Io;
                    break;

                default:
                    ret = LibUsbError.Other;
                    break;
            }

            NativeImport.FreeTransfer(transfer);

            return ret;
        }

        private static unsafe void Callback(Transfer* transfer)
        {
            int id = transfer->UserData.ToInt32();
            Transfers.TryRemove(id, out ManualResetEventSlim transferData);
            transferData.Set();
        }
    }
}

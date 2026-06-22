using System;

namespace ASD.DeviceCore.Ble.Bgapi
{
    /// <summary>
    /// Silicon Labs BGAPI binary message framing. A BGAPI message is a 4-byte header
    /// plus a payload:
    /// <code>
    ///   byte0: [7]=event?  [6:3]=device type (0b0100 = Bluetooth)  [2:0]=length high
    ///   byte1: length low (payload length = (b0&7)&lt;&lt;8 | b1)
    ///   byte2: command/event class id
    ///   byte3: command/event method id
    /// </code>
    /// Commands use a leading type bit of 0 (header byte0 0x20 for Bluetooth); events
    /// use 1 (0xA0). This framing is stable across SDK versions; the per-command class
    /// and method ids are not — those live in <see cref="BgapiIds"/>.
    /// </summary>
    public static class BgapiFrame
    {
        /// <summary>Device-type nibble for Bluetooth in header byte0 (bits 6:3).</summary>
        public const byte DeviceBluetooth = 0x20;
        public const byte EventBit = 0x80;
        public const int HeaderLen = 4;

        public static byte[] EncodeCommand(byte classId, byte methodId, ReadOnlySpan<byte> payload)
        {
            if (payload.Length > 0x7FF)
                throw new ArgumentException("BGAPI payload exceeds 2047 bytes.", nameof(payload));

            var msg = new byte[HeaderLen + payload.Length];
            msg[0] = (byte)(DeviceBluetooth | ((payload.Length >> 8) & 0x07));
            msg[1] = (byte)(payload.Length & 0xFF);
            msg[2] = classId;
            msg[3] = methodId;
            payload.CopyTo(msg.AsSpan(HeaderLen));
            return msg;
        }

        /// <summary>Parse the 4-byte header. Returns the declared payload length.</summary>
        public static BgapiHeader ParseHeader(ReadOnlySpan<byte> header)
        {
            if (header.Length < HeaderLen)
                throw new ArgumentException("Need 4 header bytes.", nameof(header));
            int len = ((header[0] & 0x07) << 8) | header[1];
            bool isEvent = (header[0] & EventBit) != 0;
            return new BgapiHeader(isEvent, header[2], header[3], len);
        }
    }

    public readonly record struct BgapiHeader(bool IsEvent, byte ClassId, byte MethodId, int PayloadLength);

    /// <summary>
    /// BGAPI class/method/event ids for the <c>sl_bt_*</c> commands the NCP central
    /// uses. <b>VALIDATE THESE against <c>sl_bt_api.h</c> of the installed Simplicity
    /// SDK before trusting on-hardware traffic</b> — the framing in
    /// <see cref="BgapiFrame"/> is stable, but these ids can shift between SDK
    /// versions. They are centralised here so a single header diff updates them all.
    /// The simulator (<see cref="SimulatedBleCentral"/>) needs none of this and is the
    /// default transport until these are bench-verified.
    /// </summary>
    public static class BgapiIds
    {
        // classes
        public const byte ClassSystem = 0x01;
        public const byte ClassScanner = 0x05;
        public const byte ClassConnection = 0x06;
        public const byte ClassGatt = 0x09;

        // sl_bt_system_*
        public const byte SystemHello = 0x00;

        // sl_bt_scanner_* (start/stop + scan_report event)
        public const byte ScannerStart = 0x03;
        public const byte ScannerStop = 0x05;
        public const byte EvtScannerScanReport = 0x00;

        // sl_bt_connection_* (open + set_max_mtu/get_mtu, close) and events
        public const byte ConnectionOpen = 0x04;
        public const byte ConnectionSetMaxMtu = 0x09;
        public const byte ConnectionClose = 0x05;
        public const byte EvtConnectionOpened = 0x00;
        public const byte EvtConnectionClosed = 0x01;
        public const byte EvtConnectionParameters = 0x02; // carries the negotiated mtu

        // sl_bt_gatt_* (discover, read, write) + events
        public const byte GattDiscoverPrimaryByUuid = 0x03;
        public const byte GattDiscoverCharacteristicsByUuid = 0x05;
        public const byte GattReadCharacteristicValue = 0x07;
        public const byte GattWriteCharacteristicValue = 0x09;
        public const byte EvtGattService = 0x00;
        public const byte EvtGattCharacteristic = 0x01;
        public const byte EvtGattCharacteristicValue = 0x02;
        public const byte EvtGattProcedureCompleted = 0x06;
    }
}

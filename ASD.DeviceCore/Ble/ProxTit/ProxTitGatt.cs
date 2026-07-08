using System;

namespace ASD.DeviceCore.Ble.ProxTit
{
    /// <summary>
    /// Wire constants for the ProxTit BLE download/modem protocol. Authoritative
    /// source: the ProxTit firmware (<c>prx_bt_service.h</c>, <c>gatt_configuration.btconf</c>)
    /// and the VT04 side (<c>proxtit.h</c>, <c>proxtit.c</c>). Keep byte-identical to
    /// those — see ProxTit/docs/ble_download_protocol.md and proxtit_i2c_contract.md.
    /// </summary>
    public static class ProxTitGatt
    {
        // ---- GATT service + characteristics (custom PRX service) ----
        public static readonly Guid Service = new("01e59576-be13-47da-9c29-354a565cc874");
        /// <summary>Control point: write a command, read/notify the reply.</summary>
        public static readonly Guid DataCp = new("8093f8a5-82ce-4fac-9fb3-f7dab64c0b74");
        /// <summary>Bulk read: encounter batch (logger mode) or modem file chunk (modem mode).</summary>
        public static readonly Guid Log = new("1d316924-9f39-47a3-a16d-a39846473eac");

        // ---- Control-point commands (gapp_cp_cmd_t.cmd) ----
        public const byte CmdGetTime = 0x09;
        public const byte CmdSetTime = 0x0A;
        public const byte CmdSetCfg = 0x10;
        public const byte CmdGetCfg = 0x11;
        public const byte CmdGetPage = 0x12;   // u32 pending encounter records

        // BLE-modem download (scenario 3). Results are delivered via a prx_log read
        // as a modem chunk [status, op, u32 offset, u16 len, data...].
        public const byte CmdModemOpen = 0x20;     // + 20-byte auth blob; reply = catalog
        public const byte CmdModemGetChunk = 0x21; // {u8 sensor, u32 index, u32 offset, u16 len}
        public const byte CmdModemFileInfo = 0x22; // {u8 sensor, u32 index} -> u32 size
        public const byte CmdModemDelete = 0x23;   // {u8 sensor, u32 index}

        // ---- Modem chunk status (proxtit_modem_chunk_t.status / PROXTIT_ST_*) ----
        public const byte StOk = 0x00;
        public const byte StEof = 0x01;     // short read / end of file
        public const byte StBusy = 0x02;    // file is being recorded right now
        public const byte StDenied = 0x03;  // operation not permitted
        public const byte StNoFile = 0xFF;

        // ---- Modem ops echoed in chunk.op (PROXTIT_OP_*) ----
        public const byte OpCatalog = 1;
        public const byte OpChunk = 2;
        public const byte OpFileInfo = 3;
        public const byte OpDelete = 4;

        /// <summary>Pseudo-sensor id for config.json (PROXTIT_STREAM_CONFIG).</summary>
        public const byte StreamConfig = 0xFE;

        /// <summary>Max data bytes a modem chunk carries (PROXTIT_MODEM_CHUNK_MAX).</summary>
        public const int ModemChunkMax = 240;

        /// <summary>Modem chunk header size: status(1)+op(1)+offset(4)+len(2).</summary>
        public const int ChunkHeaderLen = 8;

        /// <summary>Catalog stream-recording flag (PROXTIT_CAT_FLAG_RECORDING).</summary>
        public const byte CatFlagRecording = 0x01;

        /// <summary>The auth blob the central sends with MODEM_OPEN: u32 nonce + 16-byte MAC.</summary>
        public const int AuthBlobLen = 20;

        /// <summary>Map a sensor stream id to a short label (mirrors prox_streams[] on VT04).</summary>
        public static string StreamName(byte sensor) => sensor switch
        {
            0 => "gps",
            2 => "aud",
            4 => "imu",
            5 => "exg",
            6 => "cam",
            7 => "als",
            9 => "trh",
            11 => "radio",
            12 => "prox",
            StreamConfig => "config",
            _ => $"s{sensor}",
        };

        /// <summary>The on-disk filename suffix the VT04 uses for a stream (prox_streams[] templ).</summary>
        public static string StreamFileSuffix(byte sensor) => sensor switch
        {
            0 => "G.BIN",
            2 => "U.BIN",
            4 => "M.BIN",
            5 => "E.BIN",
            6 => "C.BIN",
            7 => "L.BIN",
            9 => "R.BIN",
            11 => "R.BIN",
            12 => "P.BIN",
            _ => ".BIN",
        };
    }
}

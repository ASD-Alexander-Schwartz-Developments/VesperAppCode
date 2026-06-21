using System.Text.Json;
using System.Text.Json.Serialization;
using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace VesperApp.Models
{
    
    public class ConfigProxtitDriver : ConfigurationDeviceDriver
    {
        public ConfigProxtitDriver() : base("PROXTIT", "Proxtit wireless trasceiver")
        {
            this.FileSize = 0;
        }


        [Browsable(false), JsonPropertyOrder(30)]
        public override UInt32 FileSize
        {
            get { return this.file_size; }
            set { this.file_size = value; }
        }


        // Config keys MUST match the VesperU5 firmware (params.c json_devices_attrs,
        // pushed to the ProxTit tag's I2C config registers). See the
        // proxtit_i2c_contract.md and proxtit.h register map.
        private UInt16 proxMode;
        private UInt16 beaconInterval;
        private UInt16 scanInterval;
        private UInt16 scanWindow;
        private UInt16 deltaRssi;
        private UInt16 avgSeconds;
        private Int16  rssiGate;

        [DisplayName("Mode (0=off, 1=proximity logger)"),
        CategoryAttribute("Proxtit specific Settings")]
        [JsonPropertyName("prxMode"), JsonPropertyOrder(20)]
        public UInt16 ProxMode { get => proxMode; set => this.proxMode = value; }

        [DisplayName("Beacon interval (ms)"),
        CategoryAttribute("Proxtit specific Settings")]
        [JsonPropertyName("prxBcnIval"), JsonPropertyOrder(21)]
        public UInt16 BeaconInterval { get => beaconInterval; set => this.beaconInterval = value; }

        [DisplayName("Scan interval (ms)"),
        CategoryAttribute("Proxtit specific Settings")]
        [JsonPropertyName("prxScanIval"), JsonPropertyOrder(22)]
        public UInt16 ScanInterval { get => scanInterval; set => this.scanInterval = value; }

        [DisplayName("Scan window (ms)"),
        CategoryAttribute("Proxtit specific Settings")]
        [JsonPropertyName("prxScanWin"), JsonPropertyOrder(23)]
        public UInt16 ScanWindow { get => scanWindow; set => this.scanWindow = value; }

        [DisplayName("Proximity gate (delta-RSSI)"),
        CategoryAttribute("Proxtit specific Settings")]
        [JsonPropertyName("prxDeltaRssi"), JsonPropertyOrder(24)]
        public UInt16 DeltaRssi { get => deltaRssi; set => this.deltaRssi = value; }

        [DisplayName("Session timeout (s)"),
        CategoryAttribute("Proxtit specific Settings")]
        [JsonPropertyName("prxAvgSec"), JsonPropertyOrder(25)]
        public UInt16 AvgSeconds { get => avgSeconds; set => this.avgSeconds = value; }

        [DisplayName("Host log RSSI gate (dBm, 0 = keep all)"),
        CategoryAttribute("Proxtit specific Settings")]
        [JsonPropertyName("prxRssiGate"), JsonPropertyOrder(26)]
        public Int16 RssiGate { get => rssiGate; set => this.rssiGate = value; }

    }
}

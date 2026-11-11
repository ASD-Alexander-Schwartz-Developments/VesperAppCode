namespace VesperApp.Models
{
    // EEG2 / EMG2 / ECG2 boards (VT04-EEG2 / -EMG2 / -ECG2): one digital design on a TI ADS1192, three pre-amplifier
    // gains (EEG2 x2001, EMG2 x999, ECG2 x201). VesperU5 1.22 runs its ADS1192 driver for each of these config names
    // and writes the name into the file header, so the decoder knows the pre-amp gain (ExgGain.Ads1192PreampGain).
    // The config layout (eegconf1..4, rate codes) is the EXG2 one. The v1.20 name "EAG2" is not offered: such an entry
    // is carried as read (ConfigUnknownDriver).

    public class ConfigEEG2Driver : ConfigEXG2Driver
    {
        public ConfigEEG2Driver() : base("EEG2", "EEG2 board, 2 channels (pre-amp x2001)") { }
    }

    public class ConfigEMG2Driver : ConfigEXG2Driver
    {
        public ConfigEMG2Driver() : base("EMG2", "EMG2 board, 2 channels (pre-amp x999)") { }
    }

    public class ConfigECG2Driver : ConfigEXG2Driver
    {
        public ConfigECG2Driver() : base("ECG2", "ECG2 board, 2 channels (pre-amp x201)") { }
    }
}

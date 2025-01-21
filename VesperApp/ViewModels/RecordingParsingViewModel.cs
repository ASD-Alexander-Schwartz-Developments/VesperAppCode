using Avalonia.Controls;
using Avalonia.Controls.Selection;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using VesperApp.Models;
using MsBox.Avalonia.Dto;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;
using ASDWaveLib;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using System.Collections;
using System.Diagnostics;
using System.IO;
using VesperApp.Controls;
using VesperApp.Services;
using VesperApp.Views;
using MsBox.Avalonia.Models;

namespace VesperApp.ViewModels
{
    public class RecordingParsingViewModel : ViewModelBase
    {
        public ICommand? BinaryFilesExtractor { get; }
        public ICommand? DataImporter { get; }
        public ICommand? ManualAudioParserCommand { get; }
        public ICommand? ManualMotionParserCommand { get; }
        public ICommand? ManualAlsParserCommand { get; }
        public ICommand? ManualTprhParserCommand { get; }
        public ICommand? ManualEXG48ParserCommand { get; }
        public ICommand? ManualEXG1292ParserCommand { get; }
        public ICommand? ManualLeptonParserCommand { get; }
        public ICommand? ManualGPSParserCommand { get; }




        public bool BinaryParserIsRunning
        {
            get => binaryParserIsRunning;
            set => this.RaiseAndSetIfChanged(ref binaryParserIsRunning, value);
        }

        private bool binaryParserIsRunning = false;

        public int BinaryParserPercent
        {
            get => binaryParserPercent;
            set => this.RaiseAndSetIfChanged(ref binaryParserPercent, value);
        }

        private int binaryParserPercent = 0;



        public RecordingParsingViewModel()
        {
            #region Parser Commands
            DataImporter = ReactiveCommand.CreateFromTask(RunDataImporter);

            BinaryFilesExtractor = ReactiveCommand.CreateFromTask(RunBinaryParser);

            ManualAudioParserCommand = ReactiveCommand.CreateFromTask(DecodeAudio);

            ManualMotionParserCommand = ReactiveCommand.CreateFromTask(DecodeMotionInnertial);

            ManualAlsParserCommand = ReactiveCommand.CreateFromTask(DecodeAls);

            ManualTprhParserCommand = ReactiveCommand.CreateFromTask(DecodeTprh);

            ManualEXG48ParserCommand = ReactiveCommand.CreateFromTask(DecodeEXG48);

            ManualEXG1292ParserCommand = ReactiveCommand.CreateFromTask(DecodeEXG1292);

            ManualLeptonParserCommand = ReactiveCommand.CreateFromTask(DecodeLepton);

            ManualGPSParserCommand = ReactiveCommand.CreateFromTask(ParseNanotagSnaps);
            #endregion

        }


        private async Task<bool> DecodeLepton()
        {
            bool retval = false;

            try
            {
                FilePickerOpenOptions options = new()
                {
                    Title = "Select parsed thermal camera snapshot files to convert to PNG",
                    //SuggestedStartLocation =,
                    FileTypeFilter = new List<FilePickerFileType>
                    {
                        new("Snapshot binary (.CBN) ")
                        {
                            Patterns = new[]{"*-*.CBN"},
                            MimeTypes = new[]{"bin/*"}
                        }
                    },
                    AllowMultiple = true,
                };

                Task<IReadOnlyList<IStorageFile>> dialog = App.AppTopLevel!.StorageProvider!.OpenFilePickerAsync(options);
                // ReSharper disable once VariableHidesOuterVariable Intentional
                await dialog.ContinueWith(async delegate (Task<IReadOnlyList<IStorageFile>> dialogs)
                {
                    try
                    {
                        IReadOnlyList<IStorageFile?> files = dialog.Result;
                        BinaryParserIsRunning = true;
                        BinaryParserPercent = 0;
                        double percentDelta = 100 / files.Count;
                        double percent = 0;
                        foreach (var file in files)
                        {
                            percent += percentDelta;
                            BinaryParserPercent = (int)percent;
                            //https://github.com/AvaloniaUI/Avalonia/blob/master/samples/ControlCatalog/Pages/DialogsPage.xaml.cs
                            if (file is not null)
                            {
                                string? lp = file.TryGetLocalPath();

                                if (lp is not null)
                                {
                                    string? currentDirectory = Path.GetDirectoryName(lp);
                                    string? currentFilename = Path.GetFileName(lp).ToUpper();
                                    string metadata = string.Empty;

                                    if (currentDirectory != null && currentFilename != null)
                                    {
                                        if (File.Exists(lp + ".txt"))                         /// Check if metadata exists
                                        {
                                            metadata = File.ReadAllText(lp + ".txt", Encoding.UTF8) ?? string.Empty;
                                        }

                                        byte[] databuf = File.ReadAllBytes(lp);

                                        LeptonReading lr = new LeptonReading(lp, databuf, 1024 - 16, DateTime.Now, 0, 0, LeptonFilterType.LEPTON_RAINBOW);
                                        lr.SaveAs(OutputFileType.PIC_JPG, lp);
                                    }
                                }
                            }
                        }
                        BinaryParserPercent = 100;
                        await Task.Delay(250);
                        BinaryParserIsRunning = false;

                    }
                    catch (Exception e) { Debug.WriteLine("An error has occured while trying to save the output: " + e); }
                });



            }
            catch { retval = true; }

            return retval;
        }

        private async Task<bool> RunBinaryParser()
        {
            bool retval = false;

            try
            {
                FilePickerOpenOptions options = new()
                {
                    Title = "Select binary files to extract data from...",
                    //SuggestedStartLocation =,
                    FileTypeFilter = new List<FilePickerFileType>
                    {
                        new("All binary files (.bin) ")
                        {
                            Patterns = new[]{"*.bin"},
                            MimeTypes = new[]{"bin/*"}
                        },
                        new("GPS Snap (.bin) ")
                        {
                            Patterns = new[]{"*G.bin"},
                            MimeTypes = new[]{"bin/*"}
                        },
                        new("Audio Recording (.bin) ")
                        {
                            Patterns = new[]{"*U.bin", "*U0.bin", "*U1.bin", "*U2.bin", "*U3.bin"},
                            MimeTypes = new[]{"bin/*"}
                        },
                        new("Motion (Innertial) Recording (.bin) ")
                        {
                            Patterns = new[]{"*M.bin"},
                            MimeTypes = new[]{"bin/*"}
                        },
                        new("Ambient Light Level (Lux) Recording (.bin) ")
                        {
                            Patterns = new[]{"*L.bin"},
                            MimeTypes = new[]{"bin/*"}
                        },
                        new("Temperature and Relative Humidity Recording (.bin) ")
                        {
                            Patterns = new[]{"*R.bin"},
                            MimeTypes = new[]{"bin/*"}
                        },
                        new("Biopotentials (EEG/EMG/ECG) Recording (.bin) ")
                        {
                            Patterns = new[]{"*E.bin"},
                            MimeTypes = new[]{"bin/*"}
                        },
                        new("Aux Analog sensor Recording (.bin) ")
                        {
                            Patterns = new[]{"*S.bin"},
                            MimeTypes = new[]{"bin/*"}
                        },
                        new("Proximity Recording (.bin) ")
                        {
                            Patterns = new[]{"*X.bin"},
                            MimeTypes = new[]{"bin/*"}
                        },
                        new("Thermal Camera (Lepton) (.bin) ")
                        {
                            Patterns = new[]{"*C.bin"},
                            MimeTypes = new[]{"bin/*"}
                        },
                        new("Self Log Recording (.bin) ")
                        {
                            Patterns = new[]{"*O.bin"},
                            MimeTypes = new[]{"bin/*"}
                        },
                    },
                    AllowMultiple = true,
                };

                Task<IReadOnlyList<IStorageFile>> dialog = App.AppTopLevel!.StorageProvider!.OpenFilePickerAsync(options);
                // ReSharper disable once VariableHidesOuterVariable Intentional
                await dialog.ContinueWith(async delegate (Task<IReadOnlyList<IStorageFile>> dialogs)
                {
                    try
                    {
                        IReadOnlyList<IStorageFile?> files = dialog.Result;
                        BinaryParserIsRunning = true;
                        BinaryParserPercent = 0;
                        double percentDelta = 100 / files.Count;
                        double percent = 0;
                        foreach (var file in files)
                        {
                            percent += percentDelta;
                            BinaryParserPercent = (int)percent;
                            //https://github.com/AvaloniaUI/Avalonia/blob/master/samples/ControlCatalog/Pages/DialogsPage.xaml.cs
                            if (file is not null)
                            {
                                string? lp = file.TryGetLocalPath();

                                if (lp is not null)
                                {
                                    string? currentDirectory = Path.GetDirectoryName(lp);
                                    string? currentFilename = Path.GetFileName(lp).ToUpper();

                                    if (currentDirectory != null && currentFilename != null)
                                    {
                                        if (currentFilename.Contains("G.BIN"))
                                        {
                                            string fullPathOnly = Path.GetFullPath(currentDirectory);
                                            fullPathOnly += Path.DirectorySeparatorChar + "DAT";
                                            if (Directory.Exists(fullPathOnly) == false)
                                            {
                                                Directory.CreateDirectory(fullPathOnly);
                                            }

                                            await BinaryParser.ExtractVesperSnap(lp, fullPathOnly, new TimeSpan(0, 0, 0));
                                        }
                                        else if (currentFilename.Contains("U.BIN"))
                                        {
                                            string fullPathOnly = Path.GetFullPath(currentDirectory);
                                            fullPathOnly += Path.DirectorySeparatorChar + "AUD";
                                            if (Directory.Exists(fullPathOnly) == false)
                                            {
                                                Directory.CreateDirectory(fullPathOnly);
                                            }

                                            await BinaryParser.StripSplit(lp, fullPathOnly, 0);
                                        }
                                        else if (currentFilename.Contains("U0.BIN"))
                                        {
                                            string fullPathOnly = Path.GetFullPath(currentDirectory);
                                            fullPathOnly += Path.DirectorySeparatorChar + "KOL-AUD";
                                            if (Directory.Exists(fullPathOnly) == false)
                                            {
                                                Directory.CreateDirectory(fullPathOnly);
                                            }

                                            await BinaryParser.StripSplitEx(lp, '0', fullPathOnly, 0);
                                        }
                                        else if (currentFilename.Contains("U1.BIN"))
                                        {
                                            string fullPathOnly = Path.GetFullPath(currentDirectory);
                                            fullPathOnly += Path.DirectorySeparatorChar + "KOL-AUD";
                                            if (Directory.Exists(fullPathOnly) == false)
                                            {
                                                Directory.CreateDirectory(fullPathOnly);
                                            }

                                            await BinaryParser.StripSplitEx(lp, '1', fullPathOnly, 0);
                                        }
                                        else if (currentFilename.Contains("U2.BIN"))
                                        {
                                            string fullPathOnly = Path.GetFullPath(currentDirectory);
                                            fullPathOnly += Path.DirectorySeparatorChar + "KOL-AUD";
                                            if (Directory.Exists(fullPathOnly) == false)
                                            {
                                                Directory.CreateDirectory(fullPathOnly);
                                            }

                                            await BinaryParser.StripSplitEx(lp, '2', fullPathOnly, 0);
                                        }
                                        else if (currentFilename.Contains("U3.BIN"))
                                        {
                                            string fullPathOnly = Path.GetFullPath(currentDirectory);
                                            fullPathOnly += Path.DirectorySeparatorChar + "KOL-AUD";
                                            if (Directory.Exists(fullPathOnly) == false)
                                            {
                                                Directory.CreateDirectory(fullPathOnly);
                                            }

                                            await BinaryParser.StripSplitEx(lp, '3', fullPathOnly, 0);
                                        }
                                        else if (currentFilename.Contains("M.BIN"))
                                        {
                                            string fullPathOnly = Path.GetFullPath(currentDirectory);
                                            fullPathOnly += Path.DirectorySeparatorChar + "IMU";
                                            if (Directory.Exists(fullPathOnly) == false)
                                            {
                                                Directory.CreateDirectory(fullPathOnly);
                                            }

                                            await BinaryParser.StripSplit(lp, fullPathOnly, 0);
                                        }
                                        else if (currentFilename.Contains("E.BIN"))
                                        {
                                            string fullPathOnly = Path.GetFullPath(currentDirectory);
                                            fullPathOnly += Path.DirectorySeparatorChar + "EXG";
                                            if (Directory.Exists(fullPathOnly) == false)
                                            {
                                                Directory.CreateDirectory(fullPathOnly);
                                            }

                                            await BinaryParser.StripSplit(lp, fullPathOnly, 0);
                                        }
                                        else if (currentFilename.Contains("R.BIN"))
                                        {
                                            string fullPathOnly = Path.GetFullPath(currentDirectory);
                                            fullPathOnly += Path.DirectorySeparatorChar + "TPRH";
                                            if (Directory.Exists(fullPathOnly) == false)
                                            {
                                                Directory.CreateDirectory(fullPathOnly);
                                            }

                                            await BinaryParser.StripSplit(lp, fullPathOnly, 0);
                                        }
                                        else if (currentFilename.Contains("L.BIN"))
                                        {
                                            string fullPathOnly = Path.GetFullPath(currentDirectory);
                                            fullPathOnly += Path.DirectorySeparatorChar + "ALS";
                                            if (Directory.Exists(fullPathOnly) == false)
                                            {
                                                Directory.CreateDirectory(fullPathOnly);
                                            }

                                            await BinaryParser.StripSplit(lp, fullPathOnly, 0);
                                        }
                                        else if (currentFilename.Contains("X.BIN"))
                                        {
                                            string fullPathOnly = Path.GetFullPath(currentDirectory);
                                            fullPathOnly += Path.DirectorySeparatorChar + "PRX";
                                            if (Directory.Exists(fullPathOnly) == false)
                                            {
                                                Directory.CreateDirectory(fullPathOnly);
                                            }

                                            await BinaryParser.StripSplit(lp, fullPathOnly, 0);
                                        }
                                        else if (currentFilename.Contains("O.BIN"))
                                        {
                                            string fullPathOnly = Path.GetFullPath(currentDirectory);
                                            fullPathOnly += Path.DirectorySeparatorChar + "LOG";
                                            if (Directory.Exists(fullPathOnly) == false)
                                            {
                                                Directory.CreateDirectory(fullPathOnly);
                                            }

                                            await BinaryParser.StripSplit(lp, fullPathOnly, 0);
                                        }
                                        else if (currentFilename.Contains("S.BIN"))
                                        {
                                            string fullPathOnly = Path.GetFullPath(currentDirectory);
                                            fullPathOnly += Path.DirectorySeparatorChar + "SNS";
                                            if (Directory.Exists(fullPathOnly) == false)
                                            {
                                                Directory.CreateDirectory(fullPathOnly);
                                            }

                                            await BinaryParser.StripSplit(lp, fullPathOnly, 0);
                                        }
                                        else if (currentFilename.Contains("C.BIN"))
                                        {
                                            string fullPathOnly = Path.GetFullPath(currentDirectory);
                                            fullPathOnly += Path.DirectorySeparatorChar + "THCAM";
                                            if (Directory.Exists(fullPathOnly) == false)
                                            {
                                                Directory.CreateDirectory(fullPathOnly);
                                            }

                                            await BinaryParser.StripSplit(lp, fullPathOnly, 0);
                                        }
                                    }
                                }
                            }
                        }
                        BinaryParserPercent = 100;
                        await Task.Delay(250);
                        BinaryParserIsRunning = false;

                    }
                    catch (Exception e) { Debug.WriteLine("An error has occured while trying to save the output: " + e); }
                });



            }
            catch { retval = true; }

            return retval;
        }

        private async Task<bool> DecodeAudio()
        {
            bool retval = false;

            try
            {
                FilePickerOpenOptions options = new()
                {
                    Title = "Select parsed audio binary recording files to convert to WAV...",
                    //SuggestedStartLocation =,
                    FileTypeFilter = new List<FilePickerFileType>
                    {
                        new("Audio binary (.UBN) ")
                        {
                            Patterns = new[]{"*-*.UBN"},
                            MimeTypes = new[]{"bin/*"}
                        }
                    },
                    AllowMultiple = true,
                };

                Task<IReadOnlyList<IStorageFile>> dialog = App.AppTopLevel!.StorageProvider!.OpenFilePickerAsync(options);
                // ReSharper disable once VariableHidesOuterVariable Intentional
                await dialog.ContinueWith(async delegate (Task<IReadOnlyList<IStorageFile>> dialogs)
                {
                    try
                    {
                        IReadOnlyList<IStorageFile?> files = dialog.Result;
                        BinaryParserIsRunning = true;
                        BinaryParserPercent = 0;
                        double percentDelta = 100 / files.Count;
                        double percent = 0;
                        foreach (var file in files)
                        {
                            percent += percentDelta;
                            BinaryParserPercent = (int)percent;
                            //https://github.com/AvaloniaUI/Avalonia/blob/master/samples/ControlCatalog/Pages/DialogsPage.xaml.cs
                            if (file is not null)
                            {
                                string? lp = file.TryGetLocalPath();

                                if (lp is not null)
                                {
                                    string? currentDirectory = Path.GetDirectoryName(lp);
                                    string? currentFilename = Path.GetFileName(lp).ToUpper();
                                    string metadata = string.Empty;

                                    if (currentDirectory != null && currentFilename != null)
                                    {
                                        if (File.Exists(lp + ".txt"))                         /// Check if metadata exists
                                        {
                                            metadata = File.ReadAllText(lp + ".txt", Encoding.UTF8) ?? string.Empty;
                                        }

                                        using (WaveFile wf = new WaveFile(lp, metadata))
                                        {
                                            byte[] databuf = File.ReadAllBytes(lp);

                                            wf.Open();
                                            wf.WriteWave(databuf);
                                        }
                                    }
                                }
                            }
                        }
                        BinaryParserPercent = 100;
                        await Task.Delay(250);
                        BinaryParserIsRunning = false;

                    }
                    catch (Exception e) { Debug.WriteLine("An error has occured while trying to save the output: " + e); }
                });



            }
            catch { retval = true; }

            return retval;
        }

        private async Task<bool> DecodeMotionInnertial()
        {
            bool retval = false;

            try
            {
                FilePickerOpenOptions options = new()
                {
                    Title = "Select parsed IMU10/NanoACC binary recording files to convert to CSV...",

                    FileTypeFilter = new List<FilePickerFileType>
                    {
                        new("Inertial Motion binary files (.MBN) ")
                        {
                            Patterns = new[]{"*-*.MBN"},
                            MimeTypes = new[]{"bin/*"}
                        },
                        new("Nanotag Accelerometer binary files (.ABN) ")
                        {
                            Patterns = new[]{"*.ABN"},
                            MimeTypes = new[]{"bin/*"}
                        }

                    },
                    AllowMultiple = true,
                };

                Task<IReadOnlyList<IStorageFile>> dialog = App.AppTopLevel!.StorageProvider!.OpenFilePickerAsync(options);
                // ReSharper disable once VariableHidesOuterVariable Intentional
                await dialog.ContinueWith(async delegate (Task<IReadOnlyList<IStorageFile>> dialogs)
                {
                    try
                    {
                        IReadOnlyList<IStorageFile?> files = dialog.Result;
                        double percentDelta = 100 / files.Count;
                        double percent = 0;
                        foreach (var file in files)
                        {
                            percent += percentDelta;
                            //BinaryParserPercent = (int)percent;
                            //https://github.com/AvaloniaUI/Avalonia/blob/master/samples/ControlCatalog/Pages/DialogsPage.xaml.cs
                            if (file is not null)
                            {
                                if (file.Name.ToUpper().Contains(".MBN"))
                                {
                                    string? lp = file.TryGetLocalPath();

                                    if (lp is not null)
                                    {
                                        string? currentDirectory = Path.GetDirectoryName(lp);
                                        string? currentFilename = Path.GetFileName(lp).ToUpper();
                                        string? metadata = currentFilename + ".txt";
                                        uint ms_sample = 0;

                                        if (currentDirectory != null && currentFilename != null && metadata != null)
                                        {
                                            metadata = currentDirectory + "/" + metadata;
                                            if (File.Exists(metadata))
                                            {
                                                string header_metadata = File.ReadAllText(metadata);

                                                if (header_metadata.Contains("SampleRate:"))
                                                {
                                                    string[] lines = header_metadata.Split(new char[] { '\n', '\r' });

                                                    foreach (string line in lines)
                                                    {
                                                        string l = line.Trim();

                                                        if (l.Contains("SampleRate:"))
                                                        {
                                                            string val = l.Substring(l.IndexOf(":") + 1);

                                                            if (val.Length > 0)
                                                            {
                                                                uint vv = 0;
                                                                if (uint.TryParse(val, out vv))
                                                                {
                                                                    ms_sample = 1000 / vv;
                                                                    break;
                                                                }
                                                            }
                                                        }
                                                    }
                                                }
                                            }

                                            byte[] data = File.ReadAllBytes(lp);

                                            DateTime dtStart = DateTime.Now;

                                            ArrayList arrayList = Utils.scan(currentFilename, "M%d_%d_%d_%d_%d_%d_%d");

                                            if (arrayList.Count == 7)
                                            {
                                                int? year = (int?)arrayList[0];
                                                int? month = (int?)arrayList[1];
                                                int? day = (int?)arrayList[2];
                                                int? hr = (int?)arrayList[3];
                                                int? mn = (int?)arrayList[4];
                                                int? sec = (int?)arrayList[5];
                                                int? sbs = (int?)arrayList[6];

                                                if (year != null && month != null && day != null &&
                                                        hr != null && mn != null && sec != null && sbs != null)
                                                {

                                                    dtStart = new DateTime((int)year, (int)month, (int)day, (int)hr,
                                                        (int)mn, (int)sec, (int)sbs);
                                                }
                                            }

                                            using (IMU10Parser ip = new IMU10Parser(lp, data, dtStart, 1023, ms_sample))
                                            {
                                                ip.WriteFile();
                                            }
                                        }
                                    }
                                }
                                else if (file.Name.ToUpper().Contains(".ABN"))
                                {
                                    string? lp = file.TryGetLocalPath();

                                    if (lp is not null)
                                    {
                                        byte[] data = File.ReadAllBytes(lp);

                                        DateTime dtStart = DateTime.Now;
                                        string? currentFilename = Path.GetFileName(lp).ToUpper();
                                        uint ms_sample = 0;
                                        ArrayList arrayList = Utils.scan(currentFilename, "NACC%d_%d_%d_%d_%d_%d_%d");

                                        if (arrayList.Count == 7)
                                        {
                                            int? year = (int?)arrayList[0];
                                            int? month = (int?)arrayList[1];
                                            int? day = (int?)arrayList[2];
                                            int? hr = (int?)arrayList[3];
                                            int? mn = (int?)arrayList[4];
                                            int? sec = (int?)arrayList[5];
                                            int? sbs = (int?)arrayList[6];

                                            if (year != null && month != null && day != null &&
                                                    hr != null && mn != null && sec != null && sbs != null)
                                            {

                                                dtStart = new DateTime((int)year, (int)month, (int)day, (int)hr,
                                                    (int)mn, (int)sec, (int)sbs);
                                            }
                                        }

                                        using (NanoAccParser ip = new NanoAccParser(lp, data, dtStart, 1023, ms_sample))
                                        {
                                            ip.WriteFile();
                                        }
                                    }
                                }
                            }
                        }
                        //BinaryParserPercent = 100;
                        await Task.Delay(100);
                        //BinaryParserIsRunning = false;

                    }
                    catch (Exception e) { Debug.WriteLine("An error has occured while trying to save the output: " + e); }
                });



            }
            catch { retval = true; }

            return retval;
        }

        private async Task<bool> DecodeAls()
        {
            bool retval = false;

            try
            {
                FilePickerOpenOptions options = new()
                {
                    Title = "Select parsed TPRH31 binary recording files to convert to CSV...",

                    FileTypeFilter = new List<FilePickerFileType>
                    {
                        new("Ambient Light (Lux) recording files (.LBN) ")
                        {
                            Patterns = new[]{"*-*.LBN"},
                            MimeTypes = new[]{"bin/*"}
                        }
                    },
                    AllowMultiple = true,
                };

                Task<IReadOnlyList<IStorageFile>> dialog = App.AppTopLevel!.StorageProvider!.OpenFilePickerAsync(options);
                // ReSharper disable once VariableHidesOuterVariable Intentional
                await dialog.ContinueWith(async delegate (Task<IReadOnlyList<IStorageFile>> dialogs)
                {
                    try
                    {
                        IReadOnlyList<IStorageFile?> files = dialog.Result;
                        double percentDelta = 100 / files.Count;
                        double percent = 0;
                        foreach (var file in files)
                        {
                            percent += percentDelta;
                            //BinaryParserPercent = (int)percent;
                            if (file is not null)
                            {
                                string? lp = file.TryGetLocalPath();

                                if (lp is not null)
                                {
                                    string? currentDirectory = Path.GetDirectoryName(lp);
                                    string? currentFilename = Path.GetFileName(lp).ToUpper();
                                    string? metadata = currentFilename + ".txt";
                                    uint ms_sample = 0;

                                    if (currentDirectory != null && currentFilename != null && metadata != null)
                                    {
                                        metadata = currentDirectory + "/" + metadata;
                                        if (File.Exists(metadata))
                                        {
                                            string header_metadata = File.ReadAllText(metadata);

                                            if (header_metadata.Contains("SampleRate:"))
                                            {
                                                string[] lines = header_metadata.Split(new char[] { '\n', '\r' });

                                                foreach (string line in lines)
                                                {
                                                    string l = line.Trim();

                                                    if (l.Contains("SampleRate:"))
                                                    {
                                                        string val = l.Substring(l.IndexOf(":") + 1);

                                                        if (val.Length > 0)
                                                        {
                                                            uint vv = 0;
                                                            if (uint.TryParse(val, out vv))
                                                            {
                                                                ms_sample = vv;
                                                                break;
                                                            }
                                                        }
                                                    }
                                                }
                                            }
                                        }

                                        byte[] data = File.ReadAllBytes(lp);

                                        DateTime dtStart = DateTime.Now;

                                        ArrayList arrayList = Utils.scan(currentFilename, "L%d_%d_%d_%d_%d_%d_%d");

                                        if (arrayList.Count == 7)
                                        {
                                            int? year = (int?)arrayList[0];
                                            int? month = (int?)arrayList[1];
                                            int? day = (int?)arrayList[2];
                                            int? hr = (int?)arrayList[3];
                                            int? mn = (int?)arrayList[4];
                                            int? sec = (int?)arrayList[5];
                                            int? sbs = (int?)arrayList[6];

                                            if (year != null && month != null && day != null &&
                                                    hr != null && mn != null && sec != null && sbs != null)
                                            {

                                                dtStart = new DateTime((int)year, (int)month, (int)day, (int)hr,
                                                    (int)mn, (int)sec, (int)sbs);
                                            }
                                        }

                                        using (ALSParser ip = new ALSParser(lp, data, dtStart, 1023, ms_sample))
                                        {
                                            ip.WriteFile();
                                        }
                                    }
                                }
                            }
                        }
                        //BinaryParserPercent = 100;
                        await Task.Delay(100);
                        //BinaryParserIsRunning = false;

                    }
                    catch (Exception e) { Debug.WriteLine("An error has occured while trying to save the output: " + e); }
                });



            }
            catch { retval = true; }

            return retval;
        }

        private async Task<bool> DecodeTprh()
        {
            bool retval = false;

            try
            {
                FilePickerOpenOptions options = new()
                {
                    Title = "Select parsed TPRH31 binary recording files to convert to CSV...",

                    FileTypeFilter = new List<FilePickerFileType>
                    {
                        new("Temperature/Humidity binary files (.RBN) ")
                        {
                            Patterns = new[]{"*-*.RBN"},
                            MimeTypes = new[]{"bin/*"}
                        }
                    },
                    AllowMultiple = true,
                };

                Task<IReadOnlyList<IStorageFile>> dialog = App.AppTopLevel!.StorageProvider!.OpenFilePickerAsync(options);
                // ReSharper disable once VariableHidesOuterVariable Intentional
                await dialog.ContinueWith(async delegate (Task<IReadOnlyList<IStorageFile>> dialogs)
                {
                    try
                    {
                        IReadOnlyList<IStorageFile?> files = dialog.Result;
                        double percentDelta = 100 / files.Count;
                        double percent = 0;
                        foreach (var file in files)
                        {
                            percent += percentDelta;
                            //BinaryParserPercent = (int)percent;
                            if (file is not null)
                            {
                                string? lp = file.TryGetLocalPath();

                                if (lp is not null)
                                {
                                    string? currentDirectory = Path.GetDirectoryName(lp);
                                    string? currentFilename = Path.GetFileName(lp).ToUpper();
                                    string? metadata = currentFilename + ".txt";
                                    uint ms_sample = 0;

                                    if (currentDirectory != null && currentFilename != null && metadata != null)
                                    {
                                        metadata = currentDirectory + "/" + metadata;
                                        if (File.Exists(metadata))
                                        {
                                            string header_metadata = File.ReadAllText(metadata);

                                            if (header_metadata.Contains("SampleRate:"))
                                            {
                                                string[] lines = header_metadata.Split(new char[] { '\n', '\r' });

                                                foreach (string line in lines)
                                                {
                                                    string l = line.Trim();

                                                    if (l.Contains("SampleRate:"))
                                                    {
                                                        string val = l.Substring(l.IndexOf(":") + 1);

                                                        if (val.Length > 0)
                                                        {
                                                            uint vv = 0;
                                                            if (uint.TryParse(val, out vv))
                                                            {
                                                                ms_sample = vv;
                                                                break;
                                                            }
                                                        }
                                                    }
                                                }
                                            }
                                        }

                                        byte[] data = File.ReadAllBytes(lp);

                                        DateTime dtStart = DateTime.Now;

                                        ArrayList arrayList = Utils.scan(currentFilename, "R%d_%d_%d_%d_%d_%d_%d");

                                        if (arrayList.Count == 7)
                                        {
                                            int? year = (int?)arrayList[0];
                                            int? month = (int?)arrayList[1];
                                            int? day = (int?)arrayList[2];
                                            int? hr = (int?)arrayList[3];
                                            int? mn = (int?)arrayList[4];
                                            int? sec = (int?)arrayList[5];
                                            int? sbs = (int?)arrayList[6];

                                            if (year != null && month != null && day != null &&
                                                    hr != null && mn != null && sec != null && sbs != null)
                                            {

                                                dtStart = new DateTime((int)year, (int)month, (int)day, (int)hr,
                                                    (int)mn, (int)sec, (int)sbs);
                                            }
                                        }

                                        using (TPHParser ip = new TPHParser(lp, data, dtStart, 1023, ms_sample))
                                        {
                                            ip.WriteFile();
                                        }
                                    }
                                }
                            }
                        }
                        //BinaryParserPercent = 100;
                        await Task.Delay(100);
                        //BinaryParserIsRunning = false;

                    }
                    catch (Exception e) { Debug.WriteLine("An error has occured while trying to save the output: " + e); }
                });



            }
            catch { retval = true; }

            return retval;
        }

        private async Task<bool> DecodeEXG48()
        {
            bool retval = false;

            try
            {
                FilePickerOpenOptions options = new()
                {
                    Title = "Select parsed EXG48 binary recording files to convert to CSV...",

                    FileTypeFilter = new List<FilePickerFileType>
                    {
                        new("Biopotential binary files (.EBN) ")
                        {
                            Patterns = new[]{"*-*.EBN"},
                            MimeTypes = new[]{"bin/*"}
                        }
                    },
                    AllowMultiple = true,
                };

                Task<IReadOnlyList<IStorageFile>> dialog = App.AppTopLevel!.StorageProvider!.OpenFilePickerAsync(options);
                // ReSharper disable once VariableHidesOuterVariable Intentional
                await dialog.ContinueWith(async delegate (Task<IReadOnlyList<IStorageFile>> dialogs)
                {
                    try
                    {
                        IReadOnlyList<IStorageFile?> files = dialog.Result;
                        double percentDelta = 100 / files.Count;
                        double percent = 0;
                        foreach (var file in files)
                        {
                            percent += percentDelta;
                            //BinaryParserPercent = (int)percent;
                            //https://github.com/AvaloniaUI/Avalonia/blob/master/samples/ControlCatalog/Pages/DialogsPage.xaml.cs
                            if (file is not null)
                            {
                                string? lp = file.TryGetLocalPath();

                                if (lp is not null)
                                {
                                    string? currentDirectory = Path.GetDirectoryName(lp);
                                    string? currentFilename = Path.GetFileName(lp).ToUpper();
                                    string? metadata = currentFilename + ".txt";
                                    uint us_sample = 0;

                                    if (currentDirectory != null && currentFilename != null && metadata != null)
                                    {
                                        metadata = currentDirectory + "/" + metadata;
                                        if (File.Exists(metadata))
                                        {
                                            string header_metadata = File.ReadAllText(metadata);

                                            if (header_metadata.Contains("SampleRate:"))
                                            {
                                                string[] lines = header_metadata.Split(new char[] { '\n', '\r' });

                                                foreach (string line in lines)
                                                {
                                                    string l = line.Trim();

                                                    if (l.Contains("SampleRate:"))
                                                    {
                                                        string val = l.Substring(l.IndexOf(":") + 1);

                                                        if (val.Length > 0)
                                                        {
                                                            uint vv = 0;
                                                            if (uint.TryParse(val, out vv))
                                                            {
                                                                us_sample = 1000 / vv;
                                                                break;
                                                            }
                                                        }
                                                    }
                                                }
                                            }
                                        }

                                        byte[] data = File.ReadAllBytes(lp);

                                        DateTime dtStart = DateTime.Now;

                                        ArrayList arrayList = Utils.scan(currentFilename, "E%d_%d_%d_%d_%d_%d_%d");

                                        if (arrayList.Count == 7)
                                        {
                                            int? year = (int?)arrayList[0];
                                            int? month = (int?)arrayList[1];
                                            int? day = (int?)arrayList[2];
                                            int? hr = (int?)arrayList[3];
                                            int? mn = (int?)arrayList[4];
                                            int? sec = (int?)arrayList[5];
                                            int? sbs = (int?)arrayList[6];

                                            if (year != null && month != null && day != null &&
                                                    hr != null && mn != null && sec != null && sbs != null)
                                            {

                                                dtStart = new DateTime((int)year, (int)month, (int)day, (int)hr,
                                                    (int)mn, (int)sec, (int)sbs);
                                            }
                                        }

                                        using (EXG48Parser ip = new EXG48Parser(lp, data, dtStart, 1, us_sample))
                                        {
                                            ip.WriteFile();
                                        }
                                    }
                                }
                            }
                        }
                        //BinaryParserPercent = 100;
                        await Task.Delay(100);
                        //BinaryParserIsRunning = false;

                    }
                    catch (Exception e) { Debug.WriteLine("An error has occured while trying to save the output: " + e); }
                });



            }
            catch { retval = true; }

            return retval;
        }

        private async Task<bool> DecodeEXG1292()
        {
            bool retval = false;

            try
            {
                FilePickerOpenOptions options = new()
                {
                    Title = "Select parsed EXG1292 binary recording files to convert to CSV...",

                    FileTypeFilter = new List<FilePickerFileType>
                    {
                        new("Biopotential binary files (.EBN) ")
                        {
                            Patterns = new[]{"*-*.EBN"},
                            MimeTypes = new[]{"bin/*"}
                        }
                    },
                    AllowMultiple = true,
                };

                Task<IReadOnlyList<IStorageFile>> dialog = App.AppTopLevel!.StorageProvider!.OpenFilePickerAsync(options);
                // ReSharper disable once VariableHidesOuterVariable Intentional
                await dialog.ContinueWith(async delegate (Task<IReadOnlyList<IStorageFile>> dialogs)
                {
                    try
                    {
                        IReadOnlyList<IStorageFile?> files = dialog.Result;
                        double percentDelta = 100 / files.Count;
                        double percent = 0;
                        foreach (var file in files)
                        {
                            percent += percentDelta;
                            //BinaryParserPercent = (int)percent;
                            //https://github.com/AvaloniaUI/Avalonia/blob/master/samples/ControlCatalog/Pages/DialogsPage.xaml.cs
                            if (file is not null)
                            {
                                string? lp = file.TryGetLocalPath();

                                if (lp is not null)
                                {
                                    string? currentDirectory = Path.GetDirectoryName(lp);
                                    string? currentFilename = Path.GetFileName(lp).ToUpper();
                                    string? metadata = currentFilename + ".txt";
                                    uint us_sample = 0;

                                    if (currentDirectory != null && currentFilename != null && metadata != null)
                                    {
                                        metadata = currentDirectory + "/" + metadata;
                                        if (File.Exists(metadata))
                                        {
                                            string header_metadata = File.ReadAllText(metadata);

                                            if (header_metadata.Contains("SampleRate:"))
                                            {
                                                string[] lines = header_metadata.Split(new char[] { '\n', '\r' });

                                                foreach (string line in lines)
                                                {
                                                    string l = line.Trim();

                                                    if (l.Contains("SampleRate:"))
                                                    {
                                                        string val = l.Substring(l.IndexOf(":") + 1);

                                                        if (val.Length > 0)
                                                        {
                                                            uint vv = 0;
                                                            if (uint.TryParse(val, out vv))
                                                            {
                                                                vv--;
                                                                switch (vv)
                                                                {
                                                                    case 0:
                                                                        us_sample = 1000000 / 125;
                                                                        break;
                                                                    case 1:
                                                                        us_sample = 1000000 / 250;
                                                                        break;
                                                                    case 2:
                                                                        us_sample = 1000000 / 500;
                                                                        break;
                                                                    case 3:
                                                                        us_sample = 1000000 / 1000;
                                                                        break;
                                                                    case 4:
                                                                        us_sample = 1000000 / 2000;
                                                                        break;
                                                                    case 5:
                                                                        us_sample = 1000000 / 4000;
                                                                        break;
                                                                    case 6:
                                                                        us_sample = 1000000 / 8000;
                                                                        break;
                                                                    default:
                                                                        us_sample = 1000000 / 125;
                                                                        break;
                                                                }
                                                                break;
                                                            }
                                                        }
                                                    }
                                                }
                                            }
                                        }

                                        byte[] data = File.ReadAllBytes(lp);

                                        DateTime dtStart = DateTime.Now;

                                        ArrayList arrayList = Utils.scan(currentFilename, "E%d_%d_%d_%d_%d_%d_%d");

                                        if (arrayList.Count == 7)
                                        {
                                            int? year = (int?)arrayList[0];
                                            int? month = (int?)arrayList[1];
                                            int? day = (int?)arrayList[2];
                                            int? hr = (int?)arrayList[3];
                                            int? mn = (int?)arrayList[4];
                                            int? sec = (int?)arrayList[5];
                                            int? sbs = (int?)arrayList[6];

                                            if (year != null && month != null && day != null &&
                                                    hr != null && mn != null && sec != null && sbs != null)
                                            {

                                                dtStart = new DateTime((int)year, (int)month, (int)day, (int)hr,
                                                    (int)mn, (int)sec, (int)sbs);
                                            }
                                        }

                                        using (EXG1292Parser ip = new EXG1292Parser(lp, data, dtStart, 1, us_sample))
                                        {
                                            ip.WriteFile();
                                        }
                                    }
                                }
                            }
                        }
                        //BinaryParserPercent = 100;
                        await Task.Delay(100);
                        //BinaryParserIsRunning = false;

                    }
                    catch (Exception e) { Debug.WriteLine("An error has occured while trying to save the output: " + e); }
                });



            }
            catch { retval = true; }

            return retval;
        }


        private IProgressStatus? _copyStatus;

        public IProgressStatus? CopyStatus
        {
            get => _copyStatus;
            private set
            {
                this.RaiseAndSetIfChanged(ref _copyStatus, value);
            }
        }

        private async Task<bool> RunDataImporter()
        {
            bool result = false;
            DirectoryInfo? sourceDirectoryInfo = null;
            DirectoryInfo? destinationDirectoryInfo = null;

            FolderPickerOpenOptions options = new()
            {
                Title = "Select Vesper drive to import data from",
                AllowMultiple = false,
            };

            Task<IReadOnlyList<IStorageFolder>> dialog = App.AppTopLevel!.StorageProvider!.OpenFolderPickerAsync(options);

            await dialog.ContinueWith(delegate (Task<IReadOnlyList<IStorageFolder>> dialogs)
            {
                try
                {
                    IReadOnlyList<IStorageFolder> folders = dialog.Result;
                    string? path = null;

                    if (folders.Count > 0)
                    {
                        path = folders[0].TryGetLocalPath();
                    }

                    if (path != null && path.Length > 0)
                    {
                        sourceDirectoryInfo = new DirectoryInfo(path);
                    }
                }
                catch { }
            });

            if (sourceDirectoryInfo == null)
            {
                return false;
            }

            options = new()
            {
                Title = "Select local folder to import data to",
                AllowMultiple = false,
            };

            Task<IReadOnlyList<IStorageFolder>> dialogf = App.AppTopLevel!.StorageProvider!.OpenFolderPickerAsync(options);

            await dialogf.ContinueWith(delegate (Task<IReadOnlyList<IStorageFolder>> dialogs)
            {
                try
                {
                    IReadOnlyList<IStorageFolder> folders = dialogf.Result;
                    string? path = null;

                    if (folders.Count > 0)
                    {
                        path = folders[0].TryGetLocalPath();
                    }

                    if (path != null && path.Length > 0)
                    {
                        destinationDirectoryInfo = new DirectoryInfo(path);
                    }
                }
                catch { }
            });

            if (destinationDirectoryInfo != null && sourceDirectoryInfo != null && App.MainWindow != null)
            {
                IProgressStatus progressStatus = new ProgressStatus();
                progressStatus.ProgressUpdated += HandleProgessUpdatedEvent;
                progressStatus.Finished += HandleFinishedEvent;
                progressStatus.Cancelled += HandleCancelledEvent;
                totaln = sourceDirectoryInfo.GetDirectories().Length + 1;
                totalc = 0;
                curc = 0;
                curn = 0;
                Task _cp = CopyTo(sourceDirectoryInfo, destinationDirectoryInfo, progressStatus);  // TODO: Add progress bar

                ProgressDialogWindow progressDialogW = new ProgressDialogWindow("Copy Progress", progressStatus, App.MainWindow);
                Task progressWindowTask = progressDialogW.ShowDialog(App.MainWindow);

                Dispatcher.UIThread.Post(async () =>
                {
                    try
                    {
                        await _cp;
                    }
                    catch (OperationCanceledException)
                    {
                        // handle canceled operation
                    }

                    // close the window
                    progressDialogW.Close();
                    await progressWindowTask;

                }, DispatcherPriority.Background);
            }
            else
            {
                result = false;
            }

            return await Task.FromResult(result);
        }

        #region DemonstrateEvents
        private string _progressUpdatedEventLast = "Never";
        private string _finishedEventLast = "Never";
        private string _cancelledEventLast = "Never";
        // Properties for the view and handler functions for the events to demonstrate the operation of the events.

        public string ProgressUpdatedEventLast
        {
            get => _progressUpdatedEventLast;
            set
            {
                _progressUpdatedEventLast = value;
                this.RaiseAndSetIfChanged(ref _progressUpdatedEventLast, value);
            }
        }

        public string FinishedEventLast
        {
            get => _finishedEventLast;
            set
            {
                _finishedEventLast = value;
                this.RaiseAndSetIfChanged(ref _finishedEventLast, value);
            }
        }

        public string CancelledEventLast
        {
            get => _cancelledEventLast;
            set
            {
                _cancelledEventLast = value;
                this.RaiseAndSetIfChanged(ref _cancelledEventLast, value);
            }
        }


        private int totaln = 0;
        private int totalc = 0;
        private int curn = 0;
        private int curc = 0;


        private void HandleCancelledEvent(IProgressStatus progressStatus) => CancelledEventLast = DateTime.Now.ToString();

        private void HandleFinishedEvent(IProgressStatus progressStatus) => FinishedEventLast = DateTime.Now.ToString();

        private void HandleProgessUpdatedEvent(IProgressStatus progressStatus) => ProgressUpdatedEventLast = DateTime.Now.ToString();
        #endregion

        private async Task<bool> CopyTo(DirectoryInfo source, DirectoryInfo destination, IProgressStatus progressStatus)
        {
            try
            {
                if (source.Exists)
                {
                    if (destination.Exists == false)
                    {
                        destination.Create();
                    }

                    curn = source.GetFiles().Length;
                    curc = 0;
                    totalc++;

                    bool overwriteall = false;

                    foreach (FileInfo fileInfo in source.GetFiles())
                    {
                        var to = Path.Combine(destination.FullName, fileInfo.Name);
                        bool overwrite = true;
                        bool abort = false;

                        curc++;

                        if (File.Exists(to) && overwriteall == false)
                        {
                            await Dispatcher.UIThread.InvokeAsync(async () =>
                            {
                                MessageBoxCustomParams parm = new()
                                {
                                    ButtonDefinitions = new List<ButtonDefinition>
                                    {
                                        new () { Name = "Yes All", IsDefault = true, },
                                        new () { Name = "Yes", },
                                        new () { Name = "No", },
                                        new () { Name = "Cancel", IsCancel=true }
                                    },
                                    ContentTitle = "Overwrite ?",
                                    Icon = Icon.Question,
                                    Topmost = true,
                                    ShowInCenter = true,
                                    SizeToContent = SizeToContent.WidthAndHeight,
                                    ContentHeader = "File Exists: " + fileInfo.Name,
                                    ContentMessage = to,
                                    CanResize = false,
                                    Markdown = false,
                                    WindowStartupLocation = WindowStartupLocation.CenterOwner
                                };

                                var msgbox = MessageBoxManager.GetMessageBoxCustom(parm);
                                if (msgbox != null)
                                {
                                    string dialogm = await msgbox.ShowAsync();

                                    if (dialogm.Equals("Cancel")) { abort = true; }
                                    overwriteall = (dialogm.Equals("Yes All")) ? true : false;
                                    overwrite = (dialogm.Equals("Yes")) ? true : false;
                                }
                            }, DispatcherPriority.Input);
                        }

                        if (abort == true)
                        {
                            progressStatus.IsFinished = true;
                            return false;
                        }

                        progressStatus.Update("File Copy " + to, (int)((curc / curn) * 100.0), (int)((totalc / totaln) * 100.0));

                        try
                        {
                            fileInfo.CopyTo(to, overwrite || overwriteall);
                        }
                        catch (IOException ioecx)
                        {
                            if (overwrite = !false)
                            {
                                progressStatus.IsFinished = true;
                                return false;
                            }
                        }

                        await Task.Delay(100);
                    }

                    foreach (DirectoryInfo drs in source.GetDirectories())
                    {
                        if (await CopyTo(drs, destination, progressStatus) == false)
                        {
                            progressStatus.IsFinished = true;

                            return false;
                        }
                    }
                }

                progressStatus.Update("File Copy Done", (int)((curc / curn) * 100.0), (int)((totalc / totaln) * 100.0));
                await Task.Delay(3000);
                progressStatus.Ct.ThrowIfCancellationRequested();
                progressStatus.IsFinished = true;
                return true;
            }
            catch (Exception ex)
            {
                progressStatus.IsFinished = true;
                return false;
            }
        }



        private async Task<bool> ParseNanotagSnaps()
        {
            bool result = false;

            if (OperatingSystem.IsWindows())
            {
                FolderPickerOpenOptions options = new()
                {
                    Title = "Select FOLDER containing GPS snap .dat files to decode",
                    AllowMultiple = false,
                };

                Task<IReadOnlyList<IStorageFolder>> dialog = App.AppTopLevel!.StorageProvider!.OpenFolderPickerAsync(options);

                await dialog.ContinueWith(async delegate (Task<IReadOnlyList<IStorageFolder>> dialogs)
                {
                    try
                    {
                        IReadOnlyList<IStorageFolder> folders = dialog.Result;
                        string? path = null;

                        if (folders.Count > 0)
                        {
                            path = folders[0].TryGetLocalPath();
                        }

                        if (path != null && path.Length > 0)
                        {
                            System.Diagnostics.ProcessStartInfo psi = new System.Diagnostics.ProcessStartInfo(Directory.GetCurrentDirectory() + @"\CG\GeoTag\GeoTag.exe");
                            psi.Arguments = "-t --download=\"" + path + "\" --decode=\"" + path + "\\decode\" --geotagengine=\"" + Directory.GetCurrentDirectory() + "\\CG\\GeoTagEngine\\GeoTagEngine.exe\" --pattern=snap.*.dat";

                            //psi.RedirectStandardOutput = true;
                            //psi.RedirectStandardError = true;
                            //psi.RedirectStandardInput = true;
                            psi.WindowStyle = System.Diagnostics.ProcessWindowStyle.Minimized;
                            psi.UseShellExecute = true;
                            psi.CreateNoWindow = false;
                            System.Diagnostics.Process? ischk = null;
                            System.IO.StreamReader ischkout;
                            System.IO.StreamReader ischkerr;

                            try
                            {
                                ischk = System.Diagnostics.Process.Start(psi);
                            }
                            catch (Exception excp)
                            {
                                ischk = null;
                            }

                            if (ischk != null)
                            {

                                string? error, msg = "";
                                int index1, index2, current;
                                double percent = 0;

                                //                        this.Log("Starting " + bar.Name, 1, true);

                                await Task.Delay(1000);
                                //ischkout = ischk.StandardOutput;
                                //ischkerr = ischk.StandardError;

                                bool done = false;

                                await Task.Run(async () =>
                                {
                                    while (ischk.HasExited == false && done == false)
                                    {
                                        await Task.Delay(200);

                                        /*                              await Task.Delay(1000);
                                                                        error = ischkerr.ReadLine();

                                                                        if (error != null)
                                                                        {
                                                                            if (error.Contains("Set: decode 00%")) // isolate the number of decoded files to calculate progress
                                                                            {

                                                                                index1 = error.IndexOf('(');
                                                                                index2 = error.IndexOf('/') - 1;

                                                                                if (index1 > 0)
                                                                                {
                                                                                    msg = error.Substring(index1 + 1, index2 - index1);
                                                                                    current = Int32.Parse(msg);
                                        //                                            percent = Math.Ceiling((current / total) * 100);

                                        //                                            if (Convert.ToInt32(percent) <= 99)
                                        //                                                bar.Percent = Convert.ToInt32(percent);
                                                                                }
                                                                            }
                                                                            else if (error.Contains("7Z.exe"))
                                                                            {
                                                                                done = true;
                                                                            }
                                                                            else if (error.Contains("Graceful termination complete"))
                                                                            {
                                                                                done = true;
                                                                            }
                                                                        }*/
                                    }
                                });
                            }
                            else
                            {
                                var messageBoxStandardWindow = MsBox.Avalonia.MessageBoxManager.GetMessageBoxStandard(
                                    new MessageBoxStandardParams
                                    {
                                        ButtonDefinitions = MsBox.Avalonia.Enums.ButtonEnum.Ok,
                                        ContentTitle = "GPS Snap Parser",
                                        ContentHeader = "Could not start parser",
                                        ContentMessage = "SNAP Parser executable not found or access denied",
                                        Icon = MsBox.Avalonia.Enums.Icon.Warning,
                                        WindowIcon = App.MainWindow?.Icon,
                                    });

                                await messageBoxStandardWindow.ShowWindowDialogAsync(App.MainWindow);

                            }

                            //bar.Percent = 100;
                            //this.Log(bar.Name + " Done!", 1, true);
                        }
                    }
                    catch { }
                });
            }
            else
            {
                var messageBoxStandardWindow = MessageBoxManager.GetMessageBoxStandard(
                    new MessageBoxStandardParams
                    {
                        ButtonDefinitions = MsBox.Avalonia.Enums.ButtonEnum.Ok,
                        ContentTitle = "GPS Snap Parser",
                        ContentHeader = "Could not start parser",
                        ContentMessage = "SNAP Parser is currently available only on MS Windows OS",
                        Icon = MsBox.Avalonia.Enums.Icon.Warning,
                        WindowIcon = App.MainWindow?.Icon!,
                    });

                await messageBoxStandardWindow.ShowWindowDialogAsync(App.MainWindow!);
            }

            return await Task.FromResult(result);
        }








        private static IStorageProvider? _storageProvider;
        public static IStorageProvider? StorageProvider
        {
            get
            {
                if (_storageProvider != null)
                    return _storageProvider;

                IStorageProvider? rootTopLevelStorageProvider = App.AppTopLevel?.StorageProvider;
                if (rootTopLevelStorageProvider != null)
                {
                    _storageProvider = rootTopLevelStorageProvider;
                    return _storageProvider;
                }

                //If mainWindow is available (for example for the Desktop variant), we use it to get a storage provider.
                // If not, then we try getting the provider from the root TopLevel instance. (Web, the designer preview,...)
                //TODO doesn't work. I have ho idea how to get a TopLevel instance in a Web, preview or Android/iOS environment.
                MainWindow? mainWindow = (MainWindow?)App.MainWindow;
                _storageProvider = mainWindow != null ? mainWindow.StorageProvider : null;

                if (_storageProvider == null)
                    throw new InvalidOperationException("StorageProvider platform implementation is not available.");

                return _storageProvider;
            }
            set => _storageProvider = value;
        }

    }
}

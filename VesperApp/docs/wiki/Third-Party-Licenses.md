# Third-party licenses

VesperApp is free software under the GNU LGPL v3. It is built with open-source and vendor components that remain under their own licenses. The installed application ships the complete attribution list next to the executable:

- `THIRD-PARTY-NOTICES.md` - every component with version, license and copyright notice
- `licenses/` - the full license texts (MIT, Apache-2.0, PortAudio, BSD-3-Clause, MS-PL, LGPL, FTDI)

The same files are in the source repository: [THIRD-PARTY-NOTICES.md](https://github.com/ASD-Alexander-Schwartz-Developments/VesperApp/blob/main/THIRD-PARTY-NOTICES.md).

## Main components

| Component | Purpose | License |
|---|---|---|
| Avalonia, FluentAvaloniaUI, Avalonia.ReactiveUI / ReactiveUI | Cross-platform user interface | MIT |
| Markdown.Avalonia, CalendarControl.Avalonia, MessageBox.Avalonia | Help pages, schedule editor, dialogs | MIT (Fizzler: LGPL-3.0; Svg.Custom: MS-PL) |
| Velopack | Software updates and installer | MIT |
| Microsoft .NET libraries (System.*, Microsoft.Extensions.*) | Runtime, logging, serial ports | MIT |
| PortAudio + PortAudioSharp2 | Live View audio capture (KOL USB microphone and PC inputs) | PortAudio license (MIT-style) / Apache-2.0 |
| libusb, LibUsbDotNet | USB access to the docking station and tags | LGPL-2.1 / LGPL-3.0 |
| FTDI FTD2XX_NET | FTDI-based docking-station bridge | FTDI licence (use with FTDI-based products) |
| SkiaSharp, HarfBuzzSharp, ANGLE | Rendering and text shaping | MIT / BSD-3-Clause |

Proprietary plugins (cloud sync, GNSS decoder) are distributed separately under their own terms and are not part of the open-source build.

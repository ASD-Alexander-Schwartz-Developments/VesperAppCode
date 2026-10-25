# VesperApp

A cross-platform Avalonia desktop application for interacting with Vesper devices and firmware.  
This repository targets .NET 8 and uses Avalonia UI, Velopack for updates, and several device/USB helpers.

## Features
- Device discovery and in-app firmware flashing: STM32 USB DFU (LibUsbDfu, `0483:DF11`) for Vesper / Pipistrelle through the dock and for KOL dockless (console command 64); Nanotag through its HID bootloader (`04D8:003C`)
- Firmware Upgrades page: reads the firmware release feed (`index.json`) from the CDN that the firmware repos' release CI publishes to, filtered by device target; downloads are SHA-256 verified. No GitHub API or token in the client (see `VesperApp/Services/ReleaseFeedService.cs`, `CdnConfig.cs`)
- Recordings: import, parse and decode device recordings into WAV / CSV / images
- Configuration editor and schedule management
- Live View: oscilloscope, spectrum and spectrogram of a unit streaming its microphones over USB (KOL; Vesper / Pipistrelle firmware 1.22+) or of a computer microphone
- Clock: sync a unit's RTC to internet time to the millisecond, measure its crystal drift and store a correction (KOL 1.8 / Vesper 1.22+; older firmware gets a whole-second Set Time)
- Device Tests: capability-driven pre-deployment checks (microphone health check; GNSS / RF self-test on VT04-VESPER)
- Plugins: optional packs (GNSS Decoder, GNSS RF Test) installed per user from the CDN plugin feeds, ABI-gated, applied on the next launch
- Software updates with Velopack from the CDN feed, following the ring the app was installed from (stable or beta)
- Cross-platform UI built with Avalonia

## Getting started

Prerequisites
- .NET 8 SDK
- Visual Studio 2022/2026 or VS Code
- Platform-specific native libraries for libusb (DFU flashing, dock, Nanotag)

Build and test (from the repository root)

```
dotnet build VesperApp.sln -c Release
dotnet test VesperApp.Tests/VesperApp.Tests.csproj -c Release
dotnet run --project VesperApp/VesperApp.csproj
```

A local build has no CDN origin configured, so update, firmware and plugin feeds report "unavailable"; set `VESPERAPP_CDN_BASE` to the feed origin to use them (CI injects it via `-p:CdnBaseUrl`). Self-contained publish, as the release CI does: `dotnet publish VesperApp/VesperApp.csproj -c Release -r win-x64 --self-contained true`. CI (`.github/workflows/ci.yml`) runs the tests and uploads pre-release builds on every push to master; releases are `app-release.yml` (see the header of that file).

## Third-party components and licenses

VesperApp is LGPL-3.0 (see `LICENSE`). It distributes open-source and vendor components under their own licenses - Avalonia, FluentAvalonia, ReactiveUI, Markdown.Avalonia, Velopack, PortAudio / PortAudioSharp2 (Live View audio capture), libusb / LibUsbDotNet, FTDI FTD2XX_NET and others. The complete list with copyright notices is in [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md); full license texts are in [`licenses/`](licenses/). Both are shipped with every build.

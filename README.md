# VesperApp

A cross-platform Avalonia desktop application for interacting with Vesper devices and firmware.  
This repository targets .NET 8 and uses Avalonia UI, Velopack for updates, Octokit for GitHub interaction, and several device/USB helpers.

## Features
- Device discovery and DFU firmware updater (LibUsbDfu) - Not implemented yet, but planned for future release. Currently , firmware updates can be applied using STM32CubeProgrammer with manually downloaded firmware files.
- Firmware release browser (GitHub / Octokit)
- Configuration editor and schedule management
- Update checking & applying using Velopack
- Cross-platform UI built with Avalonia

## Getting started

Prerequisites
- .NET 8 SDK
- Visual Studio 2022/2026 or VS Code
- Platform-specific native libraries for libusb if using DFU features

Build

## Third-party components and licenses

VesperApp is LGPL-3.0 (see `LICENSE`). It distributes open-source and vendor components under their own licenses - Avalonia, FluentAvalonia, ReactiveUI, Markdown.Avalonia, Velopack, PortAudio / PortAudioSharp2 (Live View audio capture), libusb / LibUsbDotNet, FTDI FTD2XX_NET and others. The complete list with copyright notices is in [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md); full license texts are in [`licenses/`](licenses/). Both are shipped with every build.

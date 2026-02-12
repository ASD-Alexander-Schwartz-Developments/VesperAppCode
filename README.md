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
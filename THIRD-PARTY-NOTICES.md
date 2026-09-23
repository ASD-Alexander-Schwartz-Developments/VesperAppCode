# Third-party notices

VesperApp is licensed under the GNU LGPL v3 (see `LICENSE`). It is built with and distributes the third-party components listed below. Each component remains under its own license; the full license texts are in the `licenses/` folder (shipped next to the application) and the files named in the tables. This file is regenerated from the NuGet package metadata of the solution (`obj/project.assets.json`) plus the vendored native components; keep it in sync when adding a dependency.

## 1. Direct dependencies (NuGet)

| Component | Version | License | Copyright | Project |
|---|---|---|---|---|
| Avalonia | 11.3.2 | MIT | Copyright 2013-2025 © The AvaloniaUI Project | https://avaloniaui.net/?utm_source=nuget&amp;utm_medium=referral&amp;utm_content=project_homepage_link |
| Avalonia.Controls.DataGrid | 11.3.2 | MIT | Copyright 2013-2025 © The AvaloniaUI Project | https://avaloniaui.net/?utm_source=nuget&amp;utm_medium=referral&amp;utm_content=project_homepage_link |
| Avalonia.Desktop | 11.3.2 | MIT | Copyright 2013-2025 © The AvaloniaUI Project | https://avaloniaui.net/?utm_source=nuget&amp;utm_medium=referral&amp;utm_content=project_homepage_link |
| Avalonia.ReactiveUI | 11.3.2 | MIT | Copyright 2013-2025 © The AvaloniaUI Project | https://avaloniaui.net/?utm_source=nuget&amp;utm_medium=referral&amp;utm_content=project_homepage_link |
| CalendarControl.Avalonia | 11.2.5 | MIT | Satial Interfaces | https://www.nuget.org/packages/CalendarControl.Avalonia |
| FluentAvaloniaUI | 2.3.0 | MIT | FluentAvalonia | https://www.nuget.org/packages/FluentAvaloniaUI |
| FTD2XX.Net | 1.2.1 | License.txt | FTDI2XX | https://ftdichip.com/software-examples/code-examples/csharp-examples/ |
| Markdown.Avalonia | 11.0.3 | MIT | Copyright (c) 2010 Bevan Arps, 2020 whistyun | https://github.com/whistyun/Markdown.Avalonia |
| MessageBox.Avalonia | 3.2.0 | MIT | Lary | https://github.com/CreateLab/MessageBox.Avalonia |
| Microsoft.Extensions.Hosting | 8.0.1 | MIT | © Microsoft Corporation. All rights reserved. | https://dot.net/ |
| Microsoft.Extensions.Logging | 8.0.1 | MIT | © Microsoft Corporation. All rights reserved. | https://dot.net/ |
| Microsoft.Extensions.Logging.Abstractions | 9.0.6 | MIT | © Microsoft Corporation. All rights reserved. | https://dot.net/ |
| PortAudioSharp2 | 1.0.6 | Apache-2.0 | Copyright 2019 | https://github.com/csukuangfj/PortAudioSharp2 |
| System.IO.Ports | 9.0.6 | MIT | © Microsoft Corporation. All rights reserved. | https://dot.net/ |
| System.Text.Json | 9.0.6 | MIT | © Microsoft Corporation. All rights reserved. | https://dot.net/ |
| Velopack | 0.0.1298 | MIT | Copyright © Velopack Ltd. All rights reserved. | https://github.com/velopack/velopack |

## 2. Native and vendored components

| Component | Used for | License | Copyright / notice | License text |
|---|---|---|---|---|
| PortAudio (native `portaudio.dll` / `libportaudio.so` / `.dylib`, shipped by the `org.k2fsa.portaudio.runtime.*` packages) | Live View audio capture (KOL USB microphone, PC inputs) | PortAudio license (MIT-style, with non-binding request) | Copyright (c) 1999-2011 Ross Bencina and Phil Burk | `licenses/PortAudio-LICENSE.txt` |
| PortAudioSharp2 (C# binding; the `org.k2fsa.portaudio.runtime.*` packaging is declared Apache-2.0) | Live View audio capture | Apache License 2.0 | Copyright 2019 Benjamin N. Summerton (binding, from Bassoon/PortAudioSharp); packaging by csukuangfj | `licenses/PortAudioSharp2-LICENSE.txt`, `licenses/Apache-2.0.txt` |
| libusb 1.0 (`libusb-1.0.dll`, MinGW build) | USB access to the ASD docking station and tags | LGPL-2.1-or-later (dynamically linked; replaceable) | Copyright (c) 2001 Johannes Erdfelt, 2007-2009 Daniel Drake, 2010-2012 Peter Stuge, 2008-2016 Nathan Hjelm, 2009-2013 Pete Batard, 2009-2013 Ludovic Rousseau, 2010-2012 Michael Plante, 2011-2013 Hans de Goede, 2012-2013 Martin Pieuchot, 2012-2013 Toby Gray, 2013-2018 Chris Dickens and others | `libusb-LICENSE.txt` |
| LibUsbDotNet (`ASDLibUSBWrapper`, `LibUsbDfu`) | USB / DFU access | LGPL-3.0 (dynamically linked) | LibUsbDotNet contributors | `LibUsbDotNet-LICENSE.txt` |
| FTDI FTD2XX_NET.dll (`FTD2XX.Net` package) | FTDI-based docking-station bridge | FTDI licence: may be used only in conjunction with products based on FTDI parts; provided "as is" | Copyright 2007-2013 Future Technology Devices International Limited | `licenses/FTD2XX.Net-LICENSE.txt` |
| ANGLE (`Avalonia.Angle.Windows.Natives`) | Avalonia GPU rendering on Windows | BSD-3-Clause | Copyright 2018 The ANGLE Project Authors | `licenses/ANGLE-BSD-3-Clause.txt` |
| SkiaSharp / HarfBuzzSharp native assets | Avalonia rendering and text shaping | MIT (bindings); Skia BSD-3-Clause, HarfBuzz "Old MIT" | Microsoft Corporation; Google Inc. (Skia); HarfBuzz authors | `licenses/MIT.txt` |
| Fizzler (via Markdown.Avalonia.Html) | Help pages HTML rendering | LGPL-3.0 (dynamically linked) | Copyright 2009 Atif Aziz, Colin Ramsay | `licenses/Fizzler-LGPL-3.0.txt` |
| Svg.Custom (via Avalonia.Svg) | SVG rendering | MS-PL | Copyright Wieslaw Soltes 2023 | `licenses/MS-PL.txt` |

## 3. Complete package inventory (direct and transitive)

Generated from the restored package graph of all projects in the solution.

| Package | Version | License | Copyright |
|---|---|---|---|
| Avalonia | 11.3.2 | MIT | Copyright 2013-2025 © The AvaloniaUI Project |
| Avalonia.Angle.Windows.Natives | 2.1.25547.20250602 | LICENSE | Copyright 2013-2025 © The AvaloniaUI Project |
| Avalonia.AvaloniaEdit | 11.0.0 | MIT | AvaloniaEdit |
| Avalonia.BuildServices | 0.0.31 | MIT | Copyright 2023-2024 © The AvaloniaUI Project |
| Avalonia.Controls.ColorPicker | 11.3.2 | MIT | Copyright 2013-2025 © The AvaloniaUI Project |
| Avalonia.Controls.DataGrid | 11.3.2 | MIT | Copyright 2013-2025 © The AvaloniaUI Project |
| Avalonia.Desktop | 11.3.2 | MIT | Copyright 2013-2025 © The AvaloniaUI Project |
| Avalonia.Diagnostics | 11.3.2 | MIT | Copyright 2013-2025 © The AvaloniaUI Project |
| Avalonia.FreeDesktop | 11.3.2 | MIT | Copyright 2013-2025 © The AvaloniaUI Project |
| Avalonia.Native | 11.3.2 | MIT | Copyright 2013-2025 © The AvaloniaUI Project |
| Avalonia.ReactiveUI | 11.3.2 | MIT | Copyright 2013-2025 © The AvaloniaUI Project |
| Avalonia.Remote.Protocol | 11.3.2 | MIT | Copyright 2013-2025 © The AvaloniaUI Project |
| Avalonia.Skia | 11.3.2 | MIT | Copyright 2013-2025 © The AvaloniaUI Project |
| Avalonia.Svg | 11.0.0 | MIT | Copyright © Wiesław Šoltés 2023 |
| Avalonia.Themes.Simple | 11.3.2 | MIT | Copyright 2013-2025 © The AvaloniaUI Project |
| Avalonia.Win32 | 11.3.2 | MIT | Copyright 2013-2025 © The AvaloniaUI Project |
| Avalonia.X11 | 11.3.2 | MIT | Copyright 2013-2025 © The AvaloniaUI Project |
| CalendarControl.Avalonia | 11.2.5 | MIT | Satial Interfaces |
| ColorDocument.Avalonia | 11.0.3 | MIT | Copyright (c) 2024 whistyun |
| ColorTextBlock.Avalonia | 11.0.3 | MIT | Copyright (c) 2020 whistyun |
| DialogHost.Avalonia | 0.8.1 | MIT | SKProCH |
| DynamicData | 8.4.1 | MIT | Copyright (c) Roland Pheasant 2011-2024 |
| ExCSS | 4.1.4 | MIT | Tyler Brinks |
| Fizzler | 1.2.1 | COPYING.txt | Copyright © 2009 Atif Aziz, Colin Ramsay. All rights reserved. Portions Copyright © 2008 Novell, Inc. |
| FluentAvaloniaUI | 2.3.0 | MIT | FluentAvalonia |
| FTD2XX.Net | 1.2.1 | License.txt | FTDI2XX |
| HarfBuzzSharp | 8.3.1.1 | MIT | © Microsoft Corporation. All rights reserved. |
| HarfBuzzSharp.NativeAssets.Linux | 8.3.1.1 | MIT | © Microsoft Corporation. All rights reserved. |
| HarfBuzzSharp.NativeAssets.macOS | 8.3.1.1 | MIT | © Microsoft Corporation. All rights reserved. |
| HarfBuzzSharp.NativeAssets.WebAssembly | 8.3.1.1 | MIT | © Microsoft Corporation. All rights reserved. |
| HarfBuzzSharp.NativeAssets.Win32 | 8.3.1.1 | MIT | © Microsoft Corporation. All rights reserved. |
| HtmlAgilityPack | 1.11.42 | MIT | Copyright © ZZZ Projects Inc. |
| Markdown.Avalonia | 11.0.3 | MIT | Copyright (c) 2010 Bevan Arps, 2020 whistyun |
| Markdown.Avalonia.Html | 11.0.3 | MIT | © Simon Baynes 2013; whistyun 2023 |
| Markdown.Avalonia.Svg | 11.0.3 | MIT | Copyright (c) 2023 grifsun, whistyun |
| Markdown.Avalonia.SyntaxHigh | 11.0.3 | MIT | Copyright (c) 2021 whistyun |
| Markdown.Avalonia.Tight | 11.0.3 | MIT | Copyright (c) 2010 Bevan Arps, 2020 whistyun |
| MessageBox.Avalonia | 3.2.0 | MIT | Lary |
| MicroCom.Runtime | 0.11.0 | MIT | Copyright 2021 © Nikita Tsukanov |
| Microsoft.Extensions.Configuration | 8.0.0 | MIT | © Microsoft Corporation. All rights reserved. |
| Microsoft.Extensions.Configuration.Abstractions | 8.0.0 | MIT | © Microsoft Corporation. All rights reserved. |
| Microsoft.Extensions.Configuration.Binder | 8.0.2 | MIT | © Microsoft Corporation. All rights reserved. |
| Microsoft.Extensions.Configuration.CommandLine | 8.0.0 | MIT | © Microsoft Corporation. All rights reserved. |
| Microsoft.Extensions.Configuration.EnvironmentVariables | 8.0.0 | MIT | © Microsoft Corporation. All rights reserved. |
| Microsoft.Extensions.Configuration.FileExtensions | 8.0.1 | MIT | © Microsoft Corporation. All rights reserved. |
| Microsoft.Extensions.Configuration.Json | 8.0.1 | MIT | © Microsoft Corporation. All rights reserved. |
| Microsoft.Extensions.Configuration.UserSecrets | 8.0.1 | MIT | © Microsoft Corporation. All rights reserved. |
| Microsoft.Extensions.DependencyInjection | 8.0.1 | MIT | © Microsoft Corporation. All rights reserved. |
| Microsoft.Extensions.DependencyInjection.Abstractions | 9.0.6 | MIT | © Microsoft Corporation. All rights reserved. |
| Microsoft.Extensions.Diagnostics | 8.0.1 | MIT | © Microsoft Corporation. All rights reserved. |
| Microsoft.Extensions.Diagnostics.Abstractions | 8.0.1 | MIT | © Microsoft Corporation. All rights reserved. |
| Microsoft.Extensions.FileProviders.Abstractions | 8.0.0 | MIT | © Microsoft Corporation. All rights reserved. |
| Microsoft.Extensions.FileProviders.Physical | 8.0.0 | MIT | © Microsoft Corporation. All rights reserved. |
| Microsoft.Extensions.FileSystemGlobbing | 8.0.0 | MIT | © Microsoft Corporation. All rights reserved. |
| Microsoft.Extensions.Hosting | 8.0.1 | MIT | © Microsoft Corporation. All rights reserved. |
| Microsoft.Extensions.Hosting.Abstractions | 8.0.1 | MIT | © Microsoft Corporation. All rights reserved. |
| Microsoft.Extensions.Logging | 8.0.1 | MIT | © Microsoft Corporation. All rights reserved. |
| Microsoft.Extensions.Logging.Abstractions | 9.0.6 | MIT | © Microsoft Corporation. All rights reserved. |
| Microsoft.Extensions.Logging.Configuration | 8.0.1 | MIT | © Microsoft Corporation. All rights reserved. |
| Microsoft.Extensions.Logging.Console | 8.0.1 | MIT | © Microsoft Corporation. All rights reserved. |
| Microsoft.Extensions.Logging.Debug | 8.0.1 | MIT | © Microsoft Corporation. All rights reserved. |
| Microsoft.Extensions.Logging.EventLog | 8.0.1 | MIT | © Microsoft Corporation. All rights reserved. |
| Microsoft.Extensions.Logging.EventSource | 8.0.1 | MIT | © Microsoft Corporation. All rights reserved. |
| Microsoft.Extensions.Options | 8.0.2 | MIT | © Microsoft Corporation. All rights reserved. |
| Microsoft.Extensions.Options.ConfigurationExtensions | 8.0.0 | MIT | © Microsoft Corporation. All rights reserved. |
| Microsoft.Extensions.Primitives | 8.0.0 | MIT | © Microsoft Corporation. All rights reserved. |
| Microsoft.NETCore.Platforms | 1.1.0 | MIT (Microsoft .NET Library) | © Microsoft Corporation.  All rights reserved. |
| Microsoft.NETCore.Targets | 1.1.0 | MIT (Microsoft .NET Library) | © Microsoft Corporation.  All rights reserved. |
| NuGet.Versioning | 6.14.0 | Apache-2.0 | © Microsoft Corporation. All rights reserved. |
| org.k2fsa.portaudio.runtime.linux-aarch64 | 1.0.6 | Apache-2.0 | portaudio |
| org.k2fsa.portaudio.runtime.linux-x64 | 1.0.6 | Apache-2.0 | portaudio |
| org.k2fsa.portaudio.runtime.osx-arm64 | 1.0.6 | Apache-2.0 | portaudio |
| org.k2fsa.portaudio.runtime.osx-x64 | 1.0.6 | Apache-2.0 | portaudio |
| org.k2fsa.portaudio.runtime.win-x64 | 1.0.6 | Apache-2.0 | portaudio |
| PortAudioSharp2 | 1.0.6 | Apache-2.0 | Copyright 2019 |
| ReactiveUI | 20.1.1 | MIT | Copyright (c) .NET Foundation and Contributors |
| runtime.android-arm.runtime.native.System.IO.Ports | 9.0.6 | MIT | © Microsoft Corporation. All rights reserved. |
| runtime.android-arm64.runtime.native.System.IO.Ports | 9.0.6 | MIT | © Microsoft Corporation. All rights reserved. |
| runtime.android-x64.runtime.native.System.IO.Ports | 9.0.6 | MIT | © Microsoft Corporation. All rights reserved. |
| runtime.android-x86.runtime.native.System.IO.Ports | 9.0.6 | MIT | © Microsoft Corporation. All rights reserved. |
| runtime.any.System.Collections | 4.3.0 | MIT (Microsoft .NET Library) | © Microsoft Corporation.  All rights reserved. |
| runtime.any.System.Globalization | 4.3.0 | MIT (Microsoft .NET Library) | © Microsoft Corporation.  All rights reserved. |
| runtime.any.System.IO | 4.3.0 | MIT (Microsoft .NET Library) | © Microsoft Corporation.  All rights reserved. |
| runtime.any.System.Reflection | 4.3.0 | MIT (Microsoft .NET Library) | © Microsoft Corporation.  All rights reserved. |
| runtime.any.System.Reflection.Primitives | 4.3.0 | MIT (Microsoft .NET Library) | © Microsoft Corporation.  All rights reserved. |
| runtime.any.System.Resources.ResourceManager | 4.3.0 | MIT (Microsoft .NET Library) | © Microsoft Corporation.  All rights reserved. |
| runtime.any.System.Runtime | 4.3.0 | MIT (Microsoft .NET Library) | © Microsoft Corporation.  All rights reserved. |
| runtime.any.System.Text.Encoding | 4.3.0 | MIT (Microsoft .NET Library) | © Microsoft Corporation.  All rights reserved. |
| runtime.any.System.Threading.Tasks | 4.3.0 | MIT (Microsoft .NET Library) | © Microsoft Corporation.  All rights reserved. |
| runtime.linux-arm.runtime.native.System.IO.Ports | 9.0.6 | MIT | © Microsoft Corporation. All rights reserved. |
| runtime.linux-arm64.runtime.native.System.IO.Ports | 9.0.6 | MIT | © Microsoft Corporation. All rights reserved. |
| runtime.linux-bionic-arm64.runtime.native.System.IO.Ports | 9.0.6 | MIT | © Microsoft Corporation. All rights reserved. |
| runtime.linux-bionic-x64.runtime.native.System.IO.Ports | 9.0.6 | MIT | © Microsoft Corporation. All rights reserved. |
| runtime.linux-musl-arm.runtime.native.System.IO.Ports | 9.0.6 | MIT | © Microsoft Corporation. All rights reserved. |
| runtime.linux-musl-arm64.runtime.native.System.IO.Ports | 9.0.6 | MIT | © Microsoft Corporation. All rights reserved. |
| runtime.linux-musl-x64.runtime.native.System.IO.Ports | 9.0.6 | MIT | © Microsoft Corporation. All rights reserved. |
| runtime.linux-x64.runtime.native.System.IO.Ports | 9.0.6 | MIT | © Microsoft Corporation. All rights reserved. |
| runtime.maccatalyst-arm64.runtime.native.System.IO.Ports | 9.0.6 | MIT | © Microsoft Corporation. All rights reserved. |
| runtime.maccatalyst-x64.runtime.native.System.IO.Ports | 9.0.6 | MIT | © Microsoft Corporation. All rights reserved. |
| runtime.native.System | 4.3.0 | MIT (Microsoft .NET Library) | © Microsoft Corporation.  All rights reserved. |
| runtime.native.System.IO.Ports | 9.0.6 | MIT | © Microsoft Corporation. All rights reserved. |
| runtime.osx-arm64.runtime.native.System.IO.Ports | 9.0.6 | MIT | © Microsoft Corporation. All rights reserved. |
| runtime.osx-x64.runtime.native.System.IO.Ports | 9.0.6 | MIT | © Microsoft Corporation. All rights reserved. |
| runtime.unix.System.Diagnostics.Debug | 4.3.0 | MIT (Microsoft .NET Library) | © Microsoft Corporation.  All rights reserved. |
| runtime.unix.System.Private.Uri | 4.3.0 | MIT (Microsoft .NET Library) | © Microsoft Corporation.  All rights reserved. |
| runtime.win.System.Diagnostics.Debug | 4.3.0 | MIT (Microsoft .NET Library) | © Microsoft Corporation.  All rights reserved. |
| ShimSkiaSharp | 1.0.0 | MIT | Copyright © Wiesław Šoltés 2023 |
| SkiaSharp | 2.88.9 | MIT | © Microsoft Corporation. All rights reserved. |
| SkiaSharp.NativeAssets.Linux | 2.88.9 | MIT | © Microsoft Corporation. All rights reserved. |
| SkiaSharp.NativeAssets.macOS | 2.88.9 | MIT | © Microsoft Corporation. All rights reserved. |
| SkiaSharp.NativeAssets.WebAssembly | 2.88.9 | MIT | © Microsoft Corporation. All rights reserved. |
| SkiaSharp.NativeAssets.Win32 | 2.88.9 | MIT | © Microsoft Corporation. All rights reserved. |
| Splat | 15.1.1 | MIT | Copyright (c) .NET Foundation and Contributors |
| Svg.Custom | 1.0.0 | MS-PL | Copyright © Wiesław Šoltés 2023 |
| Svg.Model | 1.0.0 | MIT | Copyright © Wiesław Šoltés 2023 |
| System.Collections | 4.3.0 | MIT (Microsoft .NET Library) | © Microsoft Corporation.  All rights reserved. |
| System.Collections.Immutable | 1.6.0 | MIT | © Microsoft Corporation. All rights reserved. |
| System.ComponentModel.Annotations | 5.0.0 | MIT | © Microsoft Corporation. All rights reserved. |
| System.Diagnostics.Debug | 4.3.0 | MIT (Microsoft .NET Library) | © Microsoft Corporation.  All rights reserved. |
| System.Diagnostics.DiagnosticSource | 9.0.6 | MIT | © Microsoft Corporation. All rights reserved. |
| System.Diagnostics.EventLog | 8.0.1 | MIT | © Microsoft Corporation. All rights reserved. |
| System.Globalization | 4.3.0 | MIT (Microsoft .NET Library) | © Microsoft Corporation.  All rights reserved. |
| System.IO | 4.3.0 | MIT (Microsoft .NET Library) | © Microsoft Corporation.  All rights reserved. |
| System.IO.Pipelines | 9.0.6 | MIT | © Microsoft Corporation. All rights reserved. |
| System.IO.Ports | 9.0.6 | MIT | © Microsoft Corporation. All rights reserved. |
| System.Memory | 4.5.4 | MIT | © Microsoft Corporation. All rights reserved. |
| System.ObjectModel | 4.3.0 | MIT (Microsoft .NET Library) | © Microsoft Corporation.  All rights reserved. |
| System.Private.Uri | 4.3.0 | MIT (Microsoft .NET Library) | © Microsoft Corporation.  All rights reserved. |
| System.Reactive | 6.0.1 | MIT | Copyright (c) .NET Foundation and Contributors. |
| System.Reflection | 4.3.0 | MIT (Microsoft .NET Library) | © Microsoft Corporation.  All rights reserved. |
| System.Reflection.Primitives | 4.3.0 | MIT (Microsoft .NET Library) | © Microsoft Corporation.  All rights reserved. |
| System.Resources.ResourceManager | 4.3.0 | MIT (Microsoft .NET Library) | © Microsoft Corporation.  All rights reserved. |
| System.Runtime | 4.3.0 | MIT (Microsoft .NET Library) | © Microsoft Corporation.  All rights reserved. |
| System.Text.Encoding | 4.3.0 | MIT (Microsoft .NET Library) | © Microsoft Corporation.  All rights reserved. |
| System.Text.Encodings.Web | 9.0.6 | MIT | © Microsoft Corporation. All rights reserved. |
| System.Text.Json | 9.0.6 | MIT | © Microsoft Corporation. All rights reserved. |
| System.Threading | 4.3.0 | MIT (Microsoft .NET Library) | © Microsoft Corporation.  All rights reserved. |
| System.Threading.Tasks | 4.3.0 | MIT (Microsoft .NET Library) | © Microsoft Corporation.  All rights reserved. |
| System.ValueTuple | 4.5.0 | MIT | © Microsoft Corporation.  All rights reserved. |
| Tmds.DBus.Protocol | 0.21.2 | MIT | Tom Deseyn |
| Velopack | 0.0.1298 | MIT | Copyright © Velopack Ltd. All rights reserved. |

## 4. License texts

| License | File |
|---|---|
| MIT | `licenses/MIT.txt` (template; copyright holders per component above) |
| Apache License 2.0 | `licenses/Apache-2.0.txt`, `licenses/PortAudioSharp2-LICENSE.txt` |
| PortAudio license | `licenses/PortAudio-LICENSE.txt` |
| BSD-3-Clause (ANGLE) | `licenses/ANGLE-BSD-3-Clause.txt` |
| Microsoft Public License (MS-PL) | `licenses/MS-PL.txt` |
| GNU LGPL v2.1 (libusb) | `libusb-LICENSE.txt` |
| GNU LGPL v3 (LibUsbDotNet, Fizzler, VesperApp itself) | `LibUsbDotNet-LICENSE.txt`, `licenses/Fizzler-LGPL-3.0.txt`, `LICENSE` |
| FTDI licence (FTD2XX_NET) | `licenses/FTD2XX.Net-LICENSE.txt` |

Source code for the LGPL components is available from their upstream projects (libusb: https://libusb.info, LibUsbDotNet: https://github.com/LibUsbDotNet/LibUsbDotNet, Fizzler: https://github.com/atifaziz/Fizzler); VesperApp links them dynamically and does not modify them.

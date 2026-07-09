# Linux Setup

VesperApp runs on Linux (x86-64) as a self-contained **AppImage** — no .NET installation
and no package manager required. Three things need a one-time setup that Windows does
not: the AppImage itself, **USB permissions**, and **serial port group membership**.

## 1. Install the AppImage

1. Download `VesperApp` for **Linux x64** from the release channel you were given.
   The download is a single `.AppImage` file — there is no installer; the file *is*
   the application.
2. Install FUSE if your distribution doesn't have it (AppImages need it):

   ```bash
   # Ubuntu 22.04 and newer
   sudo apt install libfuse2
   # Fedora
   sudo dnf install fuse fuse-libs
   ```

3. Put the file in a location **your user can write to** and make it executable:

   ```bash
   mkdir -p ~/Apps
   mv ~/Downloads/VesperApp*.AppImage ~/Apps/
   chmod +x ~/Apps/VesperApp*.AppImage
   ```

   A writable location matters: self-update works by replacing the AppImage file in
   place. Don't run it from a read-only mount (e.g. a VM shared folder) or a
   root-owned directory.

4. Run it — double-click it in your file manager, or launch it from a terminal.

## 2. USB device permissions (udev rules)

On Linux, plain users cannot open raw USB devices by default. Without these rules the
docking station appears in the device picker (enumeration is allowed) but **connecting
to it fails**, and firmware flashing cannot reach the DFU bootloader.

Install the VesperApp udev rules
([`docs/linux/70-vesperapp.rules`](https://github.com/ASD-Alexander-Schwartz-Developments/VesperAppCode/blob/master/VesperApp/docs/linux/70-vesperapp.rules)
in the repository):

```bash
sudo cp 70-vesperapp.rules /etc/udev/rules.d/
sudo udevadm control --reload-rules && sudo udevadm trigger
```

Then **unplug and replug** the dock and devices. The rules cover:

| Device | VID:PID |
|---|---|
| Docking station (FTDI) | `0403:6001` |
| KOL / Vesper / Pipistrelle loggers | `0483:a4f4` (legacy: `0483:5710`, `0483:570f`) |
| STM32 DFU bootloader (firmware flashing) | `0483:df11` |
| Nanotag bootloader | `04d8:fe57` |

The rules also tell **ModemManager** (which runs by default on Ubuntu/Fedora desktops)
to leave the loggers' serial port alone — without that it grabs every newly plugged
CDC device for ~30 seconds of modem probing, which blocks device detection.
If you installed an earlier version of this rules file, re-install it and replug.

## 3. Serial port access (loggers)

The loggers present a USB CDC serial port (`/dev/ttyACM*`), which belongs to the
`dialout` group on Ubuntu and Fedora. Add your user and log out/in once:

```bash
sudo usermod -aG dialout $USER
```

## 4. Verify

* Plug in the dock: `lsusb` should list `0403:6001 Future Technology Devices`.
* In VesperApp, connect to the dock — the dock controls (device power, BOOT0, reset)
  should activate.
* Dock a logger and enable device power: `lsusb` should list `0483:a4f4` and
  `/dev/ttyACM0` should appear (`ls /dev/ttyACM*`); within a few seconds the device
  shows up in VesperApp's device list.
* **Running in a VM?** The logger is a separate USB device from the dock — add a USB
  filter (VirtualBox: Devices → USB) for `0483:a4f4` too, or the guest never sees it.
* Open **Software Upgrades** and press **Check Updates** — it should report either an
  update or *"No updates available"* (not a configuration error).

## Updating

Updates work exactly like on Windows — see
[Software Updates and Plugins](Software-Updates-and-Plugins). On Linux, applying an
update atomically replaces the AppImage file and relaunches the app; your settings and
working directory are untouched.

## Notes and troubleshooting

* **"No update source is configured in this build"** — you are running a development
  build, not an official release. Official AppImages have the update origin baked in
  at release time; developers can point a local build at a feed with the
  `VESPERAPP_CDN_BASE` environment variable.
* **No FTDI driver is needed** — the dock is driven over libusb on Linux (the Windows
  D2XX driver is not used). The in-kernel `ftdi_sio` module is detached automatically
  while VesperApp controls the dock.
* **The AppImage won't start** — check FUSE is installed (step 1.2). As a fallback,
  any AppImage can be unpacked and run without FUSE:
  `./VesperApp*.AppImage --appimage-extract && squashfs-root/AppRun`
  (note: self-update does not apply in extracted mode).
* App data lives in `~/.local/share/VesperApp` (respects `$XDG_DATA_HOME`); the
  default working directory is `~/Documents/MyVesperData`.

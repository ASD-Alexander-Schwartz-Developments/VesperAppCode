# Troubleshooting and FAQ

## Dock and device connection

**The dock doesn't appear when I press Connect.**
- Try another USB cable/port (data-capable, not charge-only).
- Windows: the dock uses the FTDI driver — check Device Manager for an "FT232R USB UART" (or similar) entry; if it shows an error, reinstall the FTDI D2XX driver from ftdichip.com.
- Close other software that might hold the FTDI port open (terminal programs, previous app instances).

**The dock connects but my device isn't detected.**
- Reseat the device in the dock bay and check the contacts.
- The device may be fully discharged — leave it powered in the dock for a few minutes.
- Disconnect/reconnect the dock in the app to force a re-scan.

**My Nanotag isn't detected.**
- Nanotags connect directly over USB, not via the dock — plug the tag itself in.

## Firmware flashing

**Flashing fails with "device did not enter DFU mode".**
- Keep the device seated; retry the flash — the boot sequence is timing-sensitive.
- Windows may need a moment on first use to install the ST DFU bootloader driver; retry after the first enumeration.

**A flash was interrupted. Is the device bricked?**
- No. The DFU bootloader is in ROM and always recoverable — power-cycle in the dock and flash again.

## Recordings and decoding

**My imported files parse but I get no WAV/CSV.**
- Run **Decode** explicitly on the session folder — Auto Decode may be disabled ([Settings](Settings)).
- Check the job's console/log in the Decoding Progress panel for the failing file.

**GNSS decode says the plugin is not installed.**
- Install the GNSS decoder plugin from the **Plugins** tab, then restart the app ([Software Updates and Plugins](Software-Updates-and-Plugins)).

**Most GNSS snapshots return no fix.**
- Snapshots need sky view at record time; indoor/canopy captures legitimately fail. If *all* fail on a known-good outdoor dataset, check the GNSS job log (`logs/gnss/` in the app data folder — see *Where to find logs* below) and your internet connection (satellite aiding data is fetched online).

## Updates

**"Check Updates" reports that updates aren't available/configured.**
- Development or side-loaded builds have no release feed configured; install from an official release channel.
- Corporate proxies/firewalls must allow HTTPS to the distribution CDN.

## Where to find logs

The **app data folder** is `%LOCALAPPDATA%\VesperApp` on Windows, `~/Library/Application Support/VesperApp` on macOS and `~/.local/share/VesperApp` on Linux.

| What | Where |
|---|---|
| Application log | `logs/VesperApp_<date>.log` in the app data folder |
| Startup crash reports | `logs/crash-<timestamp>.log` in the app data folder |
| GNSS decode job logs | `logs/gnss/` in the app data folder |
| App configuration | `config.json` in the app data folder |
| Decoded data | Your working directory (default `Documents/MyVesperData` in your user profile) |

**The app doesn't start at all.**
- Since v1.0.35 a startup failure shows an error dialog and writes a
  `crash-<timestamp>.log` (see above) — attach that file when reporting.
- On a brand-new Windows machine, make sure Windows Update has run once; the FTDI
  dock driver installs through it when the dock is first plugged in (the app itself
  does not require it to start).

## Reporting an issue

Open an issue on the project's GitHub repository and attach the relevant log file, your app version (Software Upgrades tab) and the device type/firmware version involved.

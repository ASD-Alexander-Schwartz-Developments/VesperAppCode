# GNSS Decoding

Vesper and Nanotag positions work by **snapshot GNSS**: instead of running a power-hungry GNSS receiver to a live fix, the tag stores short raw RF snapshots (`G.BIN`). Back at the computer, the **GNSS decoder plugin** post-processes those snapshots into latitude/longitude fixes.

## Requirements

- The **GNSS decoder plugin** must be installed — see [Software Updates and Plugins](Software-Updates-and-Plugins). Without it, all other decoding still works; only GNSS decode is unavailable.
- An internet connection is used to fetch satellite aiding data (ephemeris) for the recording period.

## Decoding snapshots

Usually you don't need to do anything: with **Auto Decode** on, `DAT` folders produced during import are decoded automatically ([Recordings](Recordings)).

To decode manually:

1. In **Recordings**, browse to the `DAT` folder of a recording session.
2. Press **Manual GPS Parser**.
3. A GNSS job appears in the **Decoding Progress** panel showing *"Decoding snapshot X of Y…"*, a live console tail and elapsed time.

## Results

The decoder writes its output files to the recording's decode folder and reports each successful fix (time, latitude, longitude, altitude, horizontal accuracy, satellites used).

A full log of every job is kept in `logs/gnss/` under the app data folder (`%LOCALAPPDATA%\VesperApp` on Windows, `~/Library/Application Support/VesperApp` on macOS, `~/.local/share/VesperApp` on Linux) — the **Open Log** button on the job panel takes you straight to it. Include this file when reporting decode problems.

## Tips for good fixes

- Snapshots recorded with a clear view of the sky decode best; heavy canopy or indoor captures may yield no fix for some snapshots — that is expected, not an error.
- Decode runs entirely on your computer; large snapshot sets take time and benefit from letting jobs run in the background.
- If every snapshot fails to decode, verify the plugin is installed and current, then check the job log for details. See [Troubleshooting and FAQ](Troubleshooting-and-FAQ).

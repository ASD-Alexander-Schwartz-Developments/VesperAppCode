# Log Codes

The optional **LOGGER** sensor records health samples and **events** while a device records. Every event has a code such as `0x0203`. **Auto Decode** writes the events to `<file>.log.csv` in the `HEALTH` folder, one row per event: time, level, code and the name listed below. See [Recordings](Recordings) for the file itself.

The codes are **one catalogue shared by every device**: a code means the same on a KOL, a Vesper, a Pipistrelle and a Nanotag. Each device logs only the codes that apply to it; the *Devices* column says which. Codes are never renumbered.

**Levels:** *debug* = normal operation (useful to follow what happened), *warning* = something to look at, *error* = data may be missing or the recording stopped.

**Devices:** KOL (firmware 1.8 or later), Vesper and Pipistrelle (firmware with the LOGGER, 1.22 or later), Nanotag (firmware 1.35 or later). "All" = all of them.

## Start-up and resets (0x01xx)

| Code | Name in the log | Level | Devices | Meaning / what to do |
|---|---|---|---|---|
| 0x0101 | Firmware started | debug | All | Every start-up. |
| 0x0102 | Reset: power-on / brown-out | debug | All | Power was applied, or the supply dipped too low. Several in a row during a recording point to a weak battery or a loose connection. |
| 0x0103 | Reset: NRST pin | warning | All | The reset button or reset line was used. |
| 0x0104 | Reset: software | debug | All | A restart by the firmware, including every wake-up from sleep. Normal. |
| 0x0105 | Reset: watchdog | error | All | The firmware stopped responding and was restarted. A device that was recording resumes. Please report it with the log file. |
| 0x0106 | Reset: illegal low-power entry | warning | KOL, Vesper, Pipistrelle | Please report it with the log file. |
| 0x0107 | Error handler ran since the previous boot | error | All | The firmware hit an internal error and restarted. The recording does not resume. Please report it with the log file. |
| 0x0108 | Log records dropped (buffer full) | warning | All | Some events came too fast to be stored; the log is incomplete around this time. |
| 0x0109 | Reset: option bytes loaded (one-time setup) | debug | KOL | Once, at the first start after a firmware update that sets up the watchdog. |
| 0x010A | Watchdog not running (option bytes not set) | warning | KOL | The watchdog setup failed; the unit still records. Please report it. |

## Storage (0x02xx)

| Code | Name in the log | Level | Devices | Meaning / what to do |
|---|---|---|---|---|
| 0x0201 | Data file could not be created / opened | error | KOL, Vesper, Pipistrelle | Check the card (or storage) with a computer; reformat it if it shows errors. |
| 0x0202 | Write to a data file failed | error | All | Data may be missing at this time. On a KOL, use a card from the tested list (see the datasheet). |
| 0x0203 | Storage full | error | All | The storage is full. A KOL (1.8) stops recording before its card is full: all files are closed normally, then it sleeps. A Nanotag stops storing data. Download the recordings and free up space. |
| 0x0204 | Storage flush failed | warning | KOL, Vesper, Pipistrelle | Data may not have reached the card at this time. Check the card. |
| 0x0205 | NAND page read uncorrectable (ECC) | error | Nanotag | A page of the memory could not be read correctly. |
| 0x0206 | NAND page read corrected at/above the ECC threshold | warning | Nanotag | A memory block is wearing out; the device retires it when needed. |
| 0x0207 | NAND block retired | warning | Nanotag | A worn memory block is no longer used. Normal in small numbers. |
| 0x0208 | NAND bad block table repaired at boot | warning | Nanotag | The memory map was repaired at start-up. |

## Battery and power (0x03xx)

| Code | Name in the log | Level | Devices | Meaning / what to do |
|---|---|---|---|---|
| 0x0301 | Battery reading not possible | warning | All | The battery could not be measured for this sample. |
| 0x0302 | USB power attached | debug | KOL | USB power appeared during a recording (a charger does not stop it). |
| 0x0303 | USB power removed | debug | KOL | USB power went away during a recording. |
| 0x0304 | Battery low: recording stopped | error | KOL, Vesper, Pipistrelle | The battery fell below the cut-off: files closed, recording stopped, the unit sleeps and does not resume. Fit fresh batteries or charge. |

## Clock and temperature (0x04xx)

| Code | Name in the log | Level | Devices | Meaning / what to do |
|---|---|---|---|---|
| 0x0401 | MCU temperature reading not possible | warning | All | The temperature could not be measured for this sample. |
| 0x0402 | Microphone clock lock re-armed | warning | KOL | The microphone clock was locked to the crystal again. |
| 0x0403 | Microphone clock trimmed | warning | KOL | The microphone clock was adjusted. |
| 0x0404 | RTC set by the host | debug | All | The clock was set from VesperApp (or the calibration was set, on a Nanotag). |
| 0x0405 | RTC synchronised by the host | debug | KOL | The clock was adjusted from VesperApp. |

## Recording and schedule (0x05xx)

| Code | Name in the log | Level | Devices | Meaning / what to do |
|---|---|---|---|---|
| 0x0501 | Recording session started | debug | All | |
| 0x0502 | Recording session stopped | debug | All | |
| 0x0503 | Schedule entry reached | debug | All | The schedule moved to its next entry. |
| 0x0504 | Schedule finished | debug | KOL, Nanotag | A dated or relative schedule passed its last entry; the device sleeps. |
| 0x0505 | config.json refused, nothing applied | error | KOL, Vesper, Pipistrelle | The configuration file is not valid for this device (another device's file, an unknown setting, a bad value or a damaged file). Create it again with the [Configuration Editor](Configuration-Editor). |

## Audio (0x06xx, KOL)

| Code | Name in the log | Level | Devices | Meaning / what to do |
|---|---|---|---|---|
| 0x0601 | Audio window opened | debug | KOL | A recording window started. |
| 0x0602 | Audio window closed | debug | KOL | A recording window ended. |
| 0x0603 | Audio samples missed (overrun) | warning | KOL | The card fell behind and some audio was lost; the gap is marked in the file and filled with silence in the WAV. Many of them: use a faster card. |
| 0x0604 | Sound trigger event | debug | KOL | A sound-triggered recording started. |

## Sensors (0x07xx, 0x08xx)

| Code | Name in the log | Level | Devices | Meaning / what to do |
|---|---|---|---|---|
| 0x0704 | Sensor reading failed | warning | Nanotag | An add-on board (TPRH31, ALS3001D, ProxTit) did not answer one reading. |
| 0x08xx | Configured sensor not found: *sensor* | error | All | A sensor enabled in the configuration is not fitted or does not answer. The last two digits are the sensor type; the log names it. Check the add-on board and its connector. The device records without it. |

Codes 0x0701, 0x0702 and 0x0703 are reserved or no longer used.

## Watchdog (0x09xx, KOL)

The unit restarted because one of its tasks stopped making progress. A unit that was recording resumes; the files that were open at the restart may be missing their last part. Please report it with the log file.

| Code | Name in the log |
|---|---|
| 0x0900 | Reset by the watchdog supervisor: main loop stalled |
| 0x0901 | Reset by the watchdog supervisor: audio recorder stalled |
| 0x0902 | Reset by the watchdog supervisor: LOGGER stalled |
| 0x0903 | Reset by the watchdog supervisor: IMU stalled |
| 0x0904 | Reset by the watchdog supervisor: TPRH stalled |
| 0x0905 | Reset by the watchdog supervisor: ALS stalled |
| 0x090F | Reset after a CPU fault (HardFault) |

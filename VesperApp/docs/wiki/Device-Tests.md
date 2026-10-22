# Device Tests

The **Device Tests** tab bundles per-sensor hardware checks you can run before a deployment. Tests are capability-driven: only the checks a connected product actually supports are offered.

![The Device Tests tab](images/tab-device-tests.png)

*The Device Tests tab. With a device connected, the microphone health check and (on VT04-VESPER) the GNSS/RF self-test become available.*

## Microphone health check

Verifies that each microphone on the device responds with a healthy signal-to-noise ratio. Available on all products with microphones (Vesper, Pipistrelle, KOL).

**How to run it**

1. Connect the device (via the dock) and select it.
2. For KOL, choose the microphone configuration to test (1, 2 or 4 mics); single-mic products test their one microphone.
3. Press **Run**.
4. When prompted, play a **steady tone** near the device — a phone tone-generator app or a speaker works fine. The test listens for in-band test tones at 1 kHz and 4 kHz over a short 200 ms capture.

**Reading the results**

Each microphone gets a row with its status, measured SNR (dB) and noise floor:

- **OK** — all test tones exceeded the health threshold (12 dB SNR).
- **Problem** — the mic responded but below threshold; check for blocked ports, membrane damage or debris and re-run.
- **No response** — the channel returned no usable signal; the microphone or its wiring is likely faulty.

A summary line reports *"X of Y microphone(s) healthy"*. The threshold is deliberately lenient — this is a field health check, not a calibrated factory measurement.

## GNSS / RF self-test (Vesper / Nanotag)

Exercises the GNSS receive chain in the device's own firmware. This is a **bench test**: it expects a controlled RF environment (a shielded enclosure with an RF generator coupled in), and is primarily used in production and service settings.

The RF generator is driven by the **GNSS RF Test** plugin (an ADALM-Pluto transmitter), installed from the [Plugins](Software-Updates-and-Plugins) page. Without it the panel says so; **Validate Fix** still works on captures you already downloaded (it needs the GNSS Decoder plugin).

- **Check Radio** — looks for the transmitter and shows its station calibration (the per-generator frequency correction).
- **Tone self-test** — the generator transmits a continuous-wave tone and the device must detect it in an RF snapshot. Reported: pass/fail, front-end level, and the tone offset the device measured. Tone offset, generator gain, SNR threshold and snapshot length (64/128/256 ms) are adjustable.
- **Apply Offset** — after a passing tone test on a *reference* unit, adds the measured offset error to the generator's station calibration. Repeat the tone test until the error is small; recalibrate when the generator changes.
- **Positioning test** — pick a scenario (static, or a moving 1 km circle), **Prepare** it once (the plugin generates it locally, a few hundred MB), **Transmit**, capture on the device, download, then **Validate Fix**: every fix is compared against where the scenario puts it; the test passes when the median error is within the tolerance (default 500 m).

If you only need to confirm end-to-end GNSS on real sky, a normal snapshot recording outdoors followed by [GNSS Decoding](GNSS-Decoding) is the simpler route.

**Note: *All the self test features are experimental and may not be available on all devices or firmware versions.**

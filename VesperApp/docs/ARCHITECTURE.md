# ASD Host-Software Architecture (open-core)

Status: **real assemblies + modem stack + daemon landed; cloud client still stubbed.**
What exists and builds in this solution today:

- `ASD.Contracts`, `ASD.DeviceCore` — real class-library assemblies (extracted).
- `ASD.DeviceCore` BLE modem stack — `ProxTitModemClient` (catalog / fileinfo /
  chunked download / delete + HMAC session auth), an `IBleCentral` abstraction, a
  `BgapiNcpBleCentral` (BGAPI-over-UART; opcode table pending bench validation), and a
  `SimulatedBleCentral` (verified default transport).
- `ASD.BaseStation` — a runnable .NET worker daemon: scan → authenticated modem
  download → SD-first local store → upload via the platform client. Proven end-to-end
  against the simulator (auth round-trips, live-file protection, incremental download,
  durable monotonic nonce).
- GNSS decode routed through `IGnssDecoder`, now served by the **proprietary
  `ASD.Gnss` plugin over cg-gnss `geotag-cli`** — loaded at runtime from a gitignored
  `plugins/` folder by `ASD.Platform.PluginLoader`. The legacy Windows-only `CG\`
  GeoTag bundle has been **removed from this repo** (1094 files), and the decode path
  is no longer Windows-gated. With no plugin present, the stub stands and GNSS decode
  is simply unavailable.

Still **future work** (separate features on this repo and `plm-cdk`): the proprietary
`ASD.PlmClient` (real backend integration); bench-validating the BGAPI opcode table;
the backend additions below. The cloud `IPlmClient` is the stub today, so the daemon's
downloads stay on local storage.

This document is the reference for how the ASD desktop suite, the standalone Linux BLE
base-station daemon, and the open-source / proprietary split fit together.

---

## 1. Goal

Consolidate ASD's host tooling into one cross-platform (Windows / macOS / Linux)
desktop suite plus a headless Linux base-station daemon, while keeping this repo
(VesperApp) safe to open-source — i.e. **the backend API surface, credentials, and the
GNSS decoder must not live in the public tree.**

Constraints that shaped the design (from prior decisions):

- Desktop only — no mobile.
- Host does **not** do native BLE. BLE reaches the host through a UART NCP adapter
  (same approach as `proxtit-downloader-py`).
- Some features are **optional modules** gated by the user's account type.
- Backend integration is via the **plm-cdk backend API + auth**, never its web UI.
- `cg-gnss` is the cross-platform GNSS decoder (C core + Python aiding server).
- Reuse VesperApp (.NET 8 + Avalonia) as the seed shell.

---

## 2. Stack

**.NET 8 + Avalonia.** Already the VesperApp stack; runs Win/macOS/Linux (incl.
linux-arm64 for the Pi-class base station), and the same compiled `DeviceCore`
assembly is reused by both the desktop app and the daemon — one implementation of the
device protocols, never two.

`factory-sw` (PySide6) stays Python. It is a factory-floor tool with a different
audience and lifecycle; it already speaks the same REST API, so it is a peer, not a
merge target.

---

## 3. Assembly layout (target split)

```
PUBLIC repo (VesperApp — open source)
├── ASD.Shell          Avalonia UI, navigation, module host, settings
├── ASD.DeviceCore     UI-less: serial + BGAPI-NCP-over-UART transports;
│                       VT04 / KOL / ProxTit protocols; modem-download + HMAC
│                       (ported from proxtit-downloader-py); recording parsers
└── ASD.Contracts      interfaces + DTOs ONLY — the plugin boundary

PRIVATE repos (binary plugins, loaded at runtime by entitlement)
├── ASD.PlmClient      implements IPlmClient: Cognito SRP, JWT refresh, REST to
│                       /portal/* and /documents/*  (knows API URL + pool IDs)
└── ASD.Gnss           implements IGnssDecoder: drives the cg-gnss geotag-cli process
```

`ASD.Contracts` and `ASD.DeviceCore` are now **real assemblies** in the solution; the
proprietary clients are still placeholder stubs (loaded later as binary plugins):

| Assembly        | Today                                                       |
|-----------------|-------------------------------------------------------------|
| `ASD.Contracts` | real assembly — interfaces + DTOs (no deps)                 |
| `ASD.DeviceCore`| real assembly — `ASD.Platform` registry + stubs, `Ble/` modem stack, `Transport/` serial |
| `ASD.Shell`     | `VesperApp.*` (existing) + shell-side `ASD.Modules.ModuleHost` |
| `ASD.BaseStation`| real assembly — the Linux base-station daemon              |
| `ASD.PlmClient` | `ASD.Platform.Stubs.StubPlmClient` (placeholder)            |
| `ASD.Gnss`      | open build binds `ASD.Platform.Stubs.StubGnssDecoder`; the real `GeoTagCliDecoder` (cg-gnss `geotag-cli`) binds at runtime when the plugin is present |

The open-source build references only `ASD.Contracts` interfaces. The API URL, the
Cognito pool/client IDs, the REST routes, and the cg-gnss decoder live entirely in the
private assemblies. A contributor sees `IPlmClient.GetMyDevicesAsync()` — never the
URL, the auth flow, or the GNSS math.

---

## 4. The plugin boundary IS the IP boundary

One rule: **the public repo compiles and runs against `ASD.Contracts` interfaces
only; every secret ships as a binary plugin from a private repo.**

- A clean checkout of open VesperApp builds a working **local** device tool — serial
  console, mic health check, recording parse, config edit — with **no cloud and no
  GNSS plugin**.
- Dropping in the signed proprietary plugins lights up backend sync and GNSS, and the
  module menu reflects the org's entitlements.
- `ASD.DeviceCore` (device protocols + the BLE-over-UART modem download client) is
  **safe to open-source** — it is yours and the deployer's, not a secret. Publishing
  the download client is a trust asset: deployers can audit what touches their field
  data.

### GNSS exposure — resolved

The snapshot-GNSS decode previously shelled out to bundled **Windows-only** binaries
that sat inside this repo (`CG\GeoTag\GeoTag.exe`, `CG\GeoTagEngine\GeoTagEngine.exe`,
plus the Intel IPP `*.dll` payload) — both an IP-exposure and a cross-platform problem.

Now: decode goes through `IGnssDecoder`; the implementation is the proprietary
**`ASD.Gnss`** plugin that drives cg-gnss's cross-platform **`geotag-cli`**. That
plugin's project lives **outside this repo** (the cg-gnss source/binaries never enter
the public tree) and is loaded at runtime from the gitignored `plugins/` folder via
`ASD.Platform.PluginLoader` (`PluginLoadContext` keeps `ASD.Contracts` unified with the
host so the plugin's `IGnssDecoder` is the same type the shell binds). The `CG\` bundle
has been deleted from the repo. A clean checkout builds and runs without GNSS decode;
dropping `ASD.Gnss.dll` + a `geotag-cli` build into `plugins/` enables it.

The plugin loader is general: it also binds a proprietary `IPlmClient` /
`IEntitlementProvider` the same way.

### Plugin discovery, updates, and the ABI gate

Plugins update on their **own channel**, independently of the shell (which self-updates
via Velopack) and of device firmware (Octokit/GitHub). To keep those cadences separate:

- **Where they live.** `PluginLoader` scans the per-user `DataPluginsDir`
  (`%LOCALAPPDATA%/VesperApp/plugins`) first, then the `BundledPluginsDir` next to the
  binary. Packs install into the data folder so a Velopack shell update — which reconciles
  the install directory — never wipes them.
- **Compatibility is by contract ABI, not file copy.** `ASD.Contracts.ContractsAbi.Version`
  is the single coupling point. A pack ships a `plugin.json` (`PluginManifest`) declaring the
  ABI and `os-arch` it was built for; the loader rejects a pack outside
  `[MinSupported, Version]` or for the wrong platform, with a clear log line, before binding.
  Bump the ABI only on a breaking contract change — so cg-gnss can ship `geotag`/aiding fixes
  endlessly under the same ABI with no shell release.
- **Apply on next launch.** Discovery runs once at startup
  (`App.OnFrameworkInitializationCompleted`), so a freshly downloaded pack binds on the next
  run rather than hot-swapping a loaded native decoder mid-session.

**Distribution carries no client secret.** Both firmware and plugin packs are published by
CI in their **private** source repos to S3/CloudFront (the AWS key is a GitHub Actions
secret, never shipped); the client only reads a `index.json` feed and downloads assets over
plain HTTPS. `ReleaseFeedService` does the feed read + SHA-256-verified download;
`FirmwareUpgradesViewModel` and `PluginUpdateService` consume it. A committed PAT that used
to sit in `FirmwareUpgradesViewModel` has been removed — a secret in an open-source (or any
shipped) client is extractable, so the credential moved to CI. Downloads are public-read
today; a `downloadUrlResolver` hook lets backend-signed URLs gate them later with no client
change. CI templates: `docs/ci-templates/`.

---

## 5. Account-type / entitlement gating

The shell gates modules and write actions on the signed-in account. The model maps
onto what plm-cdk already has:

- Org `tier`: `free` | `paid`  → premium modules (GNSS post-processing, historical
  bulk export) shown only on `paid`.
- User `custom:portalRole`: `viewer` | `admin` → write actions (RMA, delete, config
  push) shown only to `admin`.
- A per-org entitlement string list for finer control (a future backend addition —
  see [plm-backend-additions.md](plm-backend-additions.md)).

The shell asks `IEntitlementProvider.Has("gnss.postprocess")`; the open-source build's
`OfflineEntitlementProvider` returns `false` for everything proprietary, so those
modules simply do not appear. Well-known keys: `ASD.Contracts.Entitlements`.

---

## 6. The open-core seam in code (landed)

`Platform/Contracts/`:

| Interface              | Purpose                                                        | Open-source binding         |
|------------------------|---------------------------------------------------------------|-----------------------------|
| `IPlmClient`           | Backend auth + portal devices/telemetry + presigned upload    | `StubPlmClient` (inert)     |
| `IGnssDecoder`         | Cross-platform snapshot-GNSS decode                           | `StubGnssDecoder` (inert)   |
| `IEntitlementProvider` | "Is this account allowed feature X?"                          | `OfflineEntitlementProvider`|
| `IModule`              | A loadable, entitlement-gated feature contributing nav        | none registered             |
| `IPlatformContext`     | Services handed to a module at init                          | bound to the above          |

`ASD.Platform.PlatformServices` is the composition root: holds the bound services +
the `ModuleHost`, and exposes `Bind(...)` for a future proprietary plugin loader to
swap in the real clients. `App.OnFrameworkInitializationCompleted` calls
`PlatformServices.Initialize()` before the main view model builds navigation;
`MainViewViewModel` appends `Modules.GetEntitledNavCategories(...)` to the sidebar —
**empty today**, so behaviour is unchanged.

How the real plugins activate later: a loader discovers assemblies under a `/plugins`
folder, calls `PlatformServices.Bind(realPlmClient, realGnss, backedEntitlements)` and
`PlatformServices.Modules.Register(module)` for each `IModule`, all before
`Initialize()`. No open-source code changes when that happens.

---

## 7. Linux BLE base-station daemon (future)

A headless **.NET 8 worker / systemd service** that reuses the exact same
`ASD.DeviceCore` (modem-download + HMAC) and `ASD.PlmClient` assemblies the desktop
uses — so the two can never drift.

Loop: scan for tags over the UART NCP → authenticate the modem session with the
deployer HMAC key → pull the file catalog → download new files → **write to the SD
card first (source of truth), then upload** to the backend. Upload uses the presigned
-S3 path (`POST /documents/upload-url` → `PUT` → `POST /documents/confirm`) with an
ops/ingest API key, and falls back to pure local storage when offline. The SD copy
means a connectivity outage never loses field data.

`proxtit-downloader-py` stays as the reference / debug tool; the production daemon is
.NET so the protocol has one implementation.

---

## 8. What this leaves for later (separate features)

- Extract `ASD.Contracts` / `ASD.DeviceCore` into real assemblies; private
  `ASD.PlmClient` + `ASD.Gnss` repos; signed-plugin loader.
- Implement the real backend client against `plm-cdk` `/portal/*` + `/documents/*`.
- Move GNSS decode behind `IGnssDecoder`; remove `CG\` from the public tree.
- Build the Linux base-station daemon.
- Backend additions in [plm-backend-additions.md](plm-backend-additions.md).

# Release Guide — GNSS plugin & firmware → S3/CloudFront

How to cut a release on each auto-update channel: how to trigger it, the naming conventions to
use, what CI does, and how to verify. Sister doc to [`README.md`](README.md) (one-time setup) and
[`../ARCHITECTURE.md`](../ARCHITECTURE.md) (why the split exists).

> **Public-repo note:** VesperAppCode is public and this guide names the release bucket and
> CloudFront distribution. None of it is a credential, but it is internal topology — if you'd
> rather not have it in the public tree, move this file to a private ops repo. (It is currently
> **uncommitted**.)

## Channels at a glance

| Channel | Source repo | Trigger | Lands in S3 | Feed (CloudFront) |
|---|---|---|---|---|
| GNSS decoder plugin | `cg-gnss` | tag `v*` or manual | `plugins/gnss/<ver>/` | `…/plugins/gnss/index.json` |
| Firmware — Vesper | `VesperU5` | tag `v*` | `firmware/<ver>/` | `…/firmware/index.json` (`target=vesper`) |
| Firmware — KOL | `Kol` | tag `v*` | `firmware/<ver>/` | `…/firmware/index.json` (`target=kol`) |
| Desktop shell | local `release_pack.ps1` | manual (Velopack) | Velopack channels | Velopack |

All share one bucket **`<bucket>`** (eu-central-1) behind CloudFront
**`d11eqmwet07q29.cloudfront.net`** (distribution `<distribution-id>`). Clients read the feeds over
plain HTTPS with no credentials; CI holds the only secret (the AWS key). Both firmware products
publish to the **same** `firmware/index.json`; the client tells them apart by `target`.

## Trigger a release

### GNSS plugin (cg-gnss)
```bash
git tag v1.0.1
git push origin v1.0.1
```
Or: GitHub → **Actions → "Release GNSS plugin pack" → Run workflow** → version `1.0.1` (no leading `v`).
Builds the matrix `win-x64 / win-x86 / linux-x64`, assembles a pack per arch, uploads, updates the feed.

### Firmware (VesperU5 or Kol)
```bash
git tag v2.3.0
git push origin v2.3.0
```
Builds the firmware artifact, uploads, and prepends the entry to `firmware/index.json` with this
repo's `FIRMWARE_TARGET` (`vesper` / `kol`). Tag-triggered only.

## Naming conventions (use these exactly)

| Thing | Convention | Example |
|---|---|---|
| Git tag | `vMAJOR.MINOR.PATCH` (semver, leading `v`) | `v1.0.1` |
| Version string (everywhere downstream) | the tag without `v` | `1.0.1` |
| GNSS pack asset | `gnss-plugin-<ver>-<rid>.zip` | `gnss-plugin-1.0.1-win-x64.zip` |
| GNSS S3 key | `plugins/gnss/<ver>/<asset>` | `plugins/gnss/1.0.1/gnss-plugin-1.0.1-win-x64.zip` |
| GNSS feed `target` | runtime id `os-arch` | `win-x64`, `win-x86`, `linux-x64` |
| `plugin.json.platform` | the pack's `os-arch` | `win-x64` |
| Firmware asset | `firmware-<ver>-<target>.<ext>` | `firmware-2.3.0-vesper.hex` |
| Firmware S3 key | `firmware/<ver>/<asset>` | `firmware/2.3.0/firmware-2.3.0-vesper.hex` |
| Firmware feed `target` | device key = `FIRMWARE_TARGET` | `vesper`, `kol` |

Rules:
- **Tags are immutable — never re-push a tag.** To re-release, bump the patch (`v1.0.2`). Assets are
  SHA-256-pinned in the feed, so changing an asset under an existing name makes clients *reject* it.
- `target` must match what the client filters on: GNSS = the host `os-arch`; firmware = the
  `DeviceTypes` enum name lowercased (`vesper`/`kol`/`nanotag`/`pipistrelle`). Keep `FIRMWARE_TARGET`
  in sync with that enum, or the release won't show under its device on the Firmware Upgrades page.

## Versioning & ABI (GNSS only)

Two independent dials:
- **Version** (the tag) — the release number. Bump freely; it flows into `plugin.json.version` and
  the feed entry.
- **ABI** (`ABI: "1"` in the workflow; must equal `ASD.Contracts.ContractsAbi.Version`) — the
  contract-compatibility number. Bump **only** on a breaking change to
  `IGnssDecoder`/`IPlmClient`/`IEntitlementProvider`. An ABI bump requires a coordinated VesperApp
  shell release (the loader rejects a pack whose ABI is outside `[MinSupported..Version]`). Decoder
  and aiding fixes ship endlessly under ABI 1 with **no** shell release.

## What CI does, end to end

**GNSS, per tag:**
1. build `geotag` (CMake) + freeze `cg-aiding` (PyInstaller) for each arch
2. compile `ASD.Gnss.dll` against the public VesperApp `ASD.Contracts` (checked out in the job)
3. assemble the pack (`make-pack.ps1`) and write `plugin.json`
4. zip, sha256, upload to `plugins/gnss/<ver>/`
5. `publish-feed` merges the per-arch fragments → `plugins/gnss/index.json` → CloudFront invalidate

**Firmware, per tag:** build the artifact → name + checksum → upload to `firmware/<ver>/` → prepend
the entry to `firmware/index.json` → CloudFront invalidate.

## Per-repo config (current state)

| | cg-gnss | VesperU5 | Kol |
|---|---|---|---|
| `AWS_ACCESS_KEY_ID` / `AWS_SECRET_ACCESS_KEY` (secrets) | ✅ | ✅ | ✅ |
| `RELEASE_S3_BUCKET` = `<bucket>` | ✅ | ✅ | ✅ |
| `AWS_REGION` = `eu-central-1` | ✅ | ✅ | ✅ |
| `RELEASE_CF_DISTRIBUTION_ID` = `<distribution-id>` | ✅ | ✅ | ✅ |
| `FIRMWARE_TARGET` | — | `vesper` | `kol` |
| `STM32CUBEIDE_PATH` (optional) | — | if not on PATH | if not on PATH |
| `.github/workflows/release.yml` committed | ✅ (branch `feature/release-pipeline`) | ✅ (branch) | ✅ (branch) |

## Firmware build: self-hosted STM32CubeIDE runner

VesperU5 and Kol are STM32CubeIDE managed-build projects, so their committed
`release.yml` builds the Release config **headlessly with CubeIDE** on a **self-hosted Windows
runner** — the only approach that reproduces the IDE build. (The `Release/` makefiles are local-only
and hardcode machine paths; CubeIDE regenerates them from `.cproject` on the runner.) The generic
`firmware-release.yml` template here is for *simple* firmware repos — the STM32 repos use their own
CubeIDE-specific workflow instead.

One-time runner setup (a build machine that already has CubeIDE is ideal):
1. Install on the machine: STM32CubeIDE (`stm32cubeidec.exe`), Git, PowerShell 7, AWS CLI v2.
2. Register it as a GitHub Actions **self-hosted runner** for each repo (or org-wide) with the label
   **`stm32`** (repo → Settings → Actions → Runners → New self-hosted runner).
3. If `stm32cubeidec.exe` isn't on PATH, set the `STM32CUBEIDE_PATH` repo variable to its full path.
4. Merge `feature/release-pipeline` → `main`, then `git tag vX.Y.Z && git push --tags`.

The workflow infers the CubeIDE project from the repo's `*.ioc`, builds `Release/<project>.hex`, and
publishes it as `firmware-<ver>-<FIRMWARE_TARGET>.hex`. Validate the first run manually (Actions → Run
workflow) to confirm the IDE path, the `stm32` runner label, and that the Release config emits a `.hex`.

## Verify a release
```bash
curl -s https://d11eqmwet07q29.cloudfront.net/plugins/gnss/index.json | jq .
curl -s https://d11eqmwet07q29.cloudfront.net/firmware/index.json     | jq .
```
The new version should appear (newest-first). In the app: the GNSS pack shows on the plugin-updates
page (if the account is entitled to `gnss.postprocess`); firmware shows on **Firmware Upgrades**,
filtered by the selected device type.

## Rollback
The feed merge is `unique_by(version|target)`. To pull a bad release, remove its entry from
`index.json` in S3 and invalidate, **or** ship a higher patch version. Never reuse a version to
"fix" an asset — SHA-256 verification on the client will fail. Bump the patch instead.

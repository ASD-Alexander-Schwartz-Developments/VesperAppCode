# CI release templates

Drop-in GitHub Actions workflows that publish releases to **your** S3 + CloudFront and
update the client feed (`index.json`). The client (VesperApp) reads those feeds over
plain HTTPS with **no credentials**; the only secret (AWS keys) lives in the **private
source repo's** Actions secrets — never in the shipped app.

| Template | Copy to | Publishes |
|---|---|---|
| [`firmware-release.yml`](firmware-release.yml) | `VesperU5/.github/workflows/release.yml` | device firmware → `https://<cdn>/firmware/index.json` |
| [`gnss-plugin-release.yml`](gnss-plugin-release.yml) | `cg-gnss/.github/workflows/release.yml` | GNSS plugin packs → `https://<cdn>/plugins/gnss/index.json` |

## One-time setup in each private repo

**Settings → Secrets and variables → Actions**

Secrets:
- `AWS_ACCESS_KEY_ID`, `AWS_SECRET_ACCESS_KEY` — an IAM user limited to `s3:PutObject`/`GetObject`
  on the release bucket and `cloudfront:CreateInvalidation` on the distribution.

Variables:
- `RELEASE_S3_BUCKET` — the bucket name.
- `RELEASE_CF_DISTRIBUTION_ID` — the CloudFront distribution serving it.
- `AWS_REGION` — the bucket's region.

Then fill in the **build step** (the one commented `BUILD … HERE`) with your real build,
and publish a release with `git tag vX.Y.Z && git push --tags`.

## Client side (already wired in VesperApp)

- Firmware page reads `firmware/index.json` (override the URL with `VESPERAPP_FIRMWARE_FEED`).
- `PluginUpdateService` reads `plugins/gnss/index.json`, picks the entry for the host
  `os-arch`, and stages the pack into `%LOCALAPPDATA%/VesperApp/plugins` (binds next launch).
- The GNSS pack's `plugin.json` `abi` must equal `ASD.Contracts.ContractsAbi.Version`, or
  the loader rejects it.

## Access control (later, optional)

Feeds are public-read today. To gate downloads, put the bucket behind CloudFront **signed
URLs** and have the client's `downloadUrlResolver` fetch a short-lived signed URL from your
backend (plm-cdk) after login. No other client change is needed.

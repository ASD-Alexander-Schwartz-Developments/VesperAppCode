# Desktop app release — setup & operations

Standalone setup + run guide for the **VesperApp desktop installer** pipeline (Windows + Linux),
which publishes to S3/CloudFront via Velopack. Sister doc to [`RELEASE-GUIDE.md`](RELEASE-GUIDE.md)
(firmware + GNSS plugin) and [`README.md`](README.md).
Workflow: [`.github/workflows/app-release.yml`](../../../.github/workflows/app-release.yml).

> **Public-repo note:** this guide uses placeholders (`<bucket>`, `<distribution-id>`, `<account-id>`)
> for topology — no credentials or bucket names live in the tree. Fill them from your AWS account when
> you set the repo Variables/Secrets below. (Same posture as `RELEASE-GUIDE.md`.)

---

## What it does

- Builds **self-contained installers** on a matrix: `win-x64` (windows-latest) + `linux-x64`
  (ubuntu-latest). You can't build the Linux installer from Windows, so this runs in CI on two runners.
- Publishes each to the release bucket **root** via the Velopack CLI (`vpk`) on channels
  `win-x64-stable` / `linux-x64-stable` (plus `-beta`). The root is what the in-app updater reads
  (`UpdateCheckerViewModel._updateUrl` = the CloudFront root; firmware/plugins live under their own
  `firmware/` and `plugins/` prefixes).
- **Security model:** no credentials, bucket, region, or distribution id are in the workflow file.
  Topology comes from repo **Variables**; AWS creds from repo **Secrets** (or an OIDC role). They are
  injected into the standard `AWS_*` env vars, and both `vpk` and `aws` read them from the AWS default
  credential chain — nothing is ever passed on the command line. This replaced the old local
  `release_pack.ps1`/`.cmd`, which had an inline, now-revoked access key.
- A clean CI checkout has **no `plugins/` folder**, so the installer ships as the free, open-core app —
  the proprietary GNSS pack is delivered separately, only to registered users, through the plugin feed.

---

## 1. One-time setup — GitHub → Settings → Secrets and variables → Actions

### Variables
| Name | Value |
|---|---|
| `RELEASE_S3_BUCKET` | the release bucket name (`<bucket>`) |
| `AWS_REGION` | `eu-central-1` |
| `RELEASE_CF_DISTRIBUTION_ID` | the distribution id serving `<cdn-origin>` (CloudFront console → Distributions) |

### Secrets
| Name | Value |
|---|---|
| `AWS_ACCESS_KEY_ID` | a **new** least-privilege IAM user's key (the old release key is revoked) |
| `AWS_SECRET_ACCESS_KEY` | its secret |

These are the **same** names the firmware & cg-gnss repos already use — you can mirror them.

### IAM least-privilege policy
Attach to the release IAM user (or to the OIDC role — see §3). Replace `<bucket>`, `<account-id>`,
`<distribution-id>`:

```json
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Sid": "AppReleaseS3",
      "Effect": "Allow",
      "Action": ["s3:GetObject", "s3:PutObject", "s3:DeleteObject", "s3:ListBucket"],
      "Resource": ["arn:aws:s3:::<bucket>", "arn:aws:s3:::<bucket>/*"]
    },
    {
      "Sid": "AppReleaseInvalidate",
      "Effect": "Allow",
      "Action": ["cloudfront:CreateInvalidation"],
      "Resource": "arn:aws:cloudfront::<account-id>:distribution/<distribution-id>"
    }
  ]
}
```

`s3:DeleteObject` is only needed because the workflow prunes old releases (`--keepMaxReleases 10`).

---

## 2. Cut a release

1. Bump `<Version>` in `VesperApp/VesperApp.csproj`.
2. Tag and push:
   ```bash
   git tag v1.0.29
   git push origin v1.0.29
   ```
   The release version is the tag **minus the `v`** (`v1.0.29` → `1.0.29`).

Or run it manually: **Actions → "Release app" → Run workflow** → choose the ring (`stable` / `beta`);
on a manual run the version falls back to the csproj `<Version>`.

> **Tags are immutable** — never re-push a tag. To re-release, bump the patch (`v1.0.30`); Velopack and
> the feed pin assets by version, so reusing one makes clients reject it.

---

## 3. Recommended hardening — OIDC (no stored keys)

Static keys are supported but long-lived. Preferred: GitHub OIDC, so no AWS secret ever lives in the
repo.

1. Create an IAM **role** that trusts GitHub's OIDC provider (`token.actions.githubusercontent.com`),
   conditioned on this repo, with the §1 policy attached.
2. Add a `RELEASE_ROLE_ARN` repo **Variable** = that role's ARN.
3. In `app-release.yml`, in the *Configure AWS credentials* step, delete the two
   `aws-access-key-id` / `aws-secret-access-key` lines and add:
   ```yaml
   role-to-assume: ${{ vars.RELEASE_ROLE_ARN }}
   ```
   The workflow already grants `permissions: id-token: write`, so nothing else changes.

---

## 4. Code signing — deferred (installers are currently UNSIGNED)

Intentionally not configured yet. Unsigned Windows installers trigger SmartScreen "unknown publisher"
warnings until download reputation builds. When you're ready, get a certificate and add **one** of
these to the `vpk pack` step in the workflow:

- **signtool (OV/EV `.pfx` cert):**
  `--signParams "/td sha256 /fd sha256 /tr <timestamp-url> /f <cert.pfx> /p <password>"`
  (store the cert + password as GitHub Secrets and materialise the `.pfx` on the runner at build time).
- **Azure Trusted Signing** (cheaper, nothing to store on the runner):
  `--azureTrustedSignFile metadata.json`.

Linux AppImages aren't Authenticode-signed; their integrity is the SHA recorded in the Velopack feed.

---

## 5. Verify a release

```bash
curl -s https://<cdn-origin>/releases.win-x64-stable.json | jq .
```
The new version should appear (newest-first). In the app: **Software Upgrades → Check**.

---

## Known gaps / pending

- **macOS:** this workflow builds **win + linux only**. The client references `osx-arm64-*` channels,
  but the workflow doesn't publish them — add an `osx-arm64` matrix row (on `macos-latest`) when you
  want Mac releases.
- **Linux in-app auto-update:** now wired in `UpdateCheckerViewModel` (the channel is OS-aware), so
  Linux users get both the installer and in-app updates.
- **Registered-only plugin download:** the GNSS-pack page gates on a registered session
  (`AccessContext.IsRegistered`), but real per-download enforcement is the backend **signed-URL** path
  (`downloadUrlResolver`) — that switches on with sign-in. The feed is public-read until then.

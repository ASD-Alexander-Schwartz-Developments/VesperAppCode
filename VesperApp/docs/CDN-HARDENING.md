# CDN hardening — limiting abuse of the release/plugin distribution

The VesperApp client reads everything it downloads — the Velopack self-update, the firmware and
GNSS-plugin release feeds, and on-demand GNSS scenario assets — from **one CloudFront origin**.
This note records how we keep that origin from being casually scraped, and (more importantly) how
we limit abuse of it at the edge.

## As-built (implemented 2026-07-02, eu-central-1 / us-east-1)

Both the source-obscurity (Part 1) and the edge controls (Part 2) are DONE and verified.
Concrete identifiers (distribution id/domain, bucket, OAC id, WAF ARN, account) are deliberately
NOT in this public doc — this is the same policy Part 1 applies to the source. They live in the
AWS console and in the release repos' Actions configuration (`CDN_BASE_URL` secret /
`RELEASE_S3_BUCKET`, `RELEASE_CF_DISTRIBUTION_ID` vars).

| Resource | Value |
|---|---|
| Distribution | `<distribution-id>` (`<origin>.cloudfront.net`) |
| Origin bucket | `<release-bucket>` — now **private** (Block Public Access all-on; public-read policy removed) |
| Origin Access Control | `<oac-id>` (SigV4, always sign) |
| WAF Web ACL | `VesperReleaseCDN` |
| WAF rules | `RateLimitPerIP` (Block, 1000 req / 5 min / IP); `AWSManagedRulesAmazonIpReputationList` + `AWSManagedRulesCommonRuleSet` (**Count** mode) |

Verified after cutover: CloudFront serves `plugins/gnss/*`, `releases.*` (HTTP 200); direct S3
(`<release-bucket>.s3.<region>.amazonaws.com/...`) now returns **403**.

**Follow-ups:**
- Promote the two managed rule groups from **Count → Block** after a few days of clean sampled
  traffic (WAF console → VesperReleaseCDN → Rules; watch `AWSManagedIpReputation` /
  `AWSCommonRuleSet` CloudWatch metrics first — CommonRuleSet can false-positive on binary uploads).
- `firmware/index.json` is live (2026-07-08): the historical VesperU5 releases v1.9–v1.20 were
  backfilled from the GitHub releases (assets at `firmware/<ver>/firmware-<ver>-vesper.hex`,
  SHA-256-pinned, release notes as descriptions). VesperU5 firmware serves Vesper AND Pipistrelle,
  and the client filters on a single `target` string — so each release has TWO feed entries
  (`target=vesper` + `target=pipistrelle`) pointing at the same asset. Keep doing this for new
  releases (the VesperU5 CI's single FIRMWARE_TARGET only emits the vesper entry — needs the same
  doubling). Kol (`target=kol`) and Nanotag (`target=nanotag`) feeds are separate and not yet
  backfilled.
- If you ever add a second distribution for this bucket, its ARN must be added to the bucket policy's
  `AWS:SourceArn` condition or its CloudFront requests will 403.

## Threat model — what we are and aren't defending

* **Not secret.** The GNSS plugin is a compiled binary; the feeds are version metadata. Nothing here
  is confidential. The payload being public is acceptable.
* **What we actually want:** reduce (a) casual discovery of the origin by anyone reading the public
  repo, and (b) the volume of unwanted requests hitting the distribution / bucket.
* **Hard limit — do not treat any of this as access control.** The origin is compiled into the
  shipped app (a .NET assembly) and is visible on the first network request. Anyone running the app
  can recover it with `strings`, a decompiler, or a proxy. Client-side gating (the "registered users
  only" download button) is UX, not enforcement. Real per-user gating would require the origin to be
  private and a backend to mint short-lived signed URLs (the `downloadUrlResolver` seam in
  `ReleaseFeedService` exists for exactly that, but is not wired up).

## Part 1 — keep the origin out of the public source (obscurity)

Implemented. The origin is no longer a literal anywhere in the repo:

* `Services/CdnConfig.cs` resolves the origin at runtime, in order:
  1. `VESPERAPP_CDN_BASE` env var (whole origin) — for local dev / CI test rigs;
  2. the build-time `[AssemblyMetadata("CdnBaseUrl")]` value;
  3. empty → update/download features degrade gracefully ("no update source configured").
  Per-asset env overrides (`VESPERAPP_GNSS_FEED`, `VESPERAPP_FIRMWARE_FEED`,
  `VESPERAPP_GNSS_SCENARIO_URL`) still win at their call sites.
* `VesperApp.csproj` surfaces `$(CdnBaseUrl)` as the assembly-metadata attribute.
* `.github/workflows/app-release.yml` passes `-p:CdnBaseUrl=${{ secrets.CDN_BASE_URL }}` on publish.

### One-time: add the CI secret

GitHub → repo → **Settings → Secrets and variables → Actions**:

* Add **`CDN_BASE_URL`** = the CloudFront origin, e.g. `https://xxxx.cloudfront.net` (no trailing slash).
* It is not sensitive — a repo **Variable** works equally well and avoids log-masking quirks. A
  Secret is used only to keep the value out of the source. If you switch to a Variable, change the
  workflow reference from `secrets.CDN_BASE_URL` to `vars.CDN_BASE_URL`.

A local `dotnet build`/`publish` without the property produces a build with no origin: the app runs,
but update/download pages show "no update source configured" unless you export `VESPERAPP_CDN_BASE`.

## Part 2 — limit abuse at the edge (the load-bearing part)

Obscurity only slows discovery. These edge controls are what actually cap request volume and keep
the bucket unreachable directly. Do these in the AWS account that owns the distribution.

### 2a. Confirm S3 is private and only reachable via CloudFront (OAC)

- [ ] **Block Public Access** is ON for the bucket (S3 console → bucket → Permissions → *Block all
      public access* = On).
- [ ] The distribution uses an **Origin Access Control** (OAC) — newer than OAI — for the S3 origin
      (CloudFront console → distribution → Origins → the S3 origin → *Origin access* = Origin access
      control settings).
- [ ] The **bucket policy** allows `s3:GetObject` only to the CloudFront service principal for this
      distribution, e.g.:
      ```json
      {
        "Effect": "Allow",
        "Principal": { "Service": "cloudfront.amazonaws.com" },
        "Action": "s3:GetObject",
        "Resource": "arn:aws:s3:::<bucket>/*",
        "Condition": { "StringEquals": {
          "AWS:SourceArn": "arn:aws:cloudfront::<account-id>:distribution/<distribution-id>" } }
      }
      ```
- [ ] Verify: the CloudFront URL serves the feed; the raw S3 URL
      (`https://<bucket>.s3.<region>.amazonaws.com/...`) returns **403**. If the S3 URL still works,
      OAC/Block-Public-Access isn't in effect and "attempts on the bucket" are still possible.

### 2b. Attach AWS WAF with a rate-based rule (the direct answer to "limit hack attempts")

- [ ] Create a **Web ACL** (scope: **CloudFront / Global, us-east-1**) and associate it with the
      distribution.
- [ ] Add a **rate-based rule**: aggregate by IP, limit e.g. **1000 requests / 5 min** (tune to real
      client behaviour — a normal client polls a feed and downloads a pack occasionally, so this is
      generous), action **Block**.
- [ ] Add **AWSManagedRulesAmazonIpReputationList** and **AWSManagedRulesCommonRuleSet** (start in
      *Count* mode, promote to *Block* once you've confirmed no false positives on the feed/asset paths).
- [ ] Note: WAF on CloudFront has a per-request cost — fine at this traffic, but watch the bill if
      volume grows.

### 2c. Cheap extra limits (optional)

- [ ] **Cache the feeds** at the edge (short TTL, e.g. 60s) so repeated polls are served by CloudFront
      and never reach S3. Invalidate on publish (the workflow already invalidates `releases.*.json`;
      add `/firmware/index.json` and `/plugins/gnss/*` if you cache those).
- [ ] **Geo-restriction** if the user base is regional (CloudFront → distribution → Security /
      Restrictions) — blunt but free.
- [ ] Set **`s3:PutObject` / delete** permissions on the CI IAM identity only (already scoped per the
      release workflow header); clients never need write.

## Rotating the origin

Because the origin is only in the CI secret + the shipped binary, rotating it (new distribution) is:
update `CDN_BASE_URL`, cut a new release. Already-installed clients keep pointing at the old origin
until they self-update, so keep the old distribution alive through at least one release cycle.

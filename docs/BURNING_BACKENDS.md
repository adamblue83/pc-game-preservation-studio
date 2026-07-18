# Burning & ISO Backends

**Status: ISO creation (Phase 6) and disc burning (Phase 7) are both
implemented.** This document records the risk assessment and approach taken,
so the design decisions are visible.

## ISO creation (Phase 6 — implemented)

- The only realistic first-party UDF image writer on Windows is
  `oscdimg.exe`, which ships in the Windows ADK's Deployment Tools
  component.
- `oscdimg.exe`'s license notice states it is licensed only for producing
  Microsoft-authorized content. **This project never bundles or
  redistributes `oscdimg.exe`.** `PcGamePreservationStudio.Burning`'s
  `OscdimgLocator` only *detects* a user-installed copy — standard ADK
  install paths (x86/amd64/arm64 under `Windows Kits\10\Assessment and
  Deployment Kit\Deployment Tools`), the `PATH` environment variable, or a
  user-configured path in Settings — and `OscdimgIsoBuilder` reports a clear
  "not found, here's how to get it" message (with a link to the ADK
  installer) rather than failing silently or crashing.
- All process invocation goes through `ProcessStartInfo.ArgumentList` (never
  a shell string) to avoid injection risk, with stdout/stderr captured for
  diagnostics. `oscdimg` is invoked with `-m` (ignore the 4 GB single-file
  size limit — game files routinely exceed it), `-u2` (joint UDF + ISO
  9660, UDF revision 1.02 — the broadest-compatibility UDF revision oscdimg
  supports), and `-h` (include hidden files and directories).
- **The `-h` flag was added after a real defect surfaced via Burn Disc's
  read-back verification, not by inspection.** `oscdimg` silently drops
  hidden files by default. GOG Galaxy marks its own per-game bookkeeping
  files (`goggame-<id>.hashdb`/`.info`/`.script`) as Hidden, so every GOG
  ISO built before this fix was silently missing those three files — while
  the archive's own `Checksums\SHA256SUMS.txt` (generated from the staged
  folder, which *did* have them) still listed them as expected. Verify Disc
  correctly reported `Failed` with those exact three files as missing after
  a real burn to real hardware, which is what caught this. This is the
  outcome the "always independently re-verify, never trust a success
  return value" design throughout this app is meant to produce.
- Selecting the `IsoOnly` media type in Create Archive builds the normal
  folder-based archive first (same checksums/metadata as any other archive),
  then converts that staged folder into a single ISO. If ISO creation
  succeeds, the staged folder is deleted — the ISO is the only remaining
  output. If oscdimg isn't found or the build fails for any reason, the
  staged folder is kept intact instead and `ArchiveBuildResult.IsoBuildWarning`
  explains why, so a failed ISO build never costs the user their archive.
- ISO-level verification (re-hashing files from inside a built ISO) is not
  implemented yet — see `IArchiveVerificationService`'s status in
  [ARCHITECTURE.md](ARCHITECTURE.md). When oscdimg isn't found, the kept
  folder is verified normally instead.
- Selecting a physical optical-disc media type (CD-R, DVD-5/9, BD-25/50/100/128)
  gets the same ISO treatment, once per disc: after multi-disc splitting,
  `ArchiveBuilder` converts each `DISC_NN\` folder into `DISC_NN.iso` so the
  whole archive is immediately ready to hand to Burn Disc, disc by disc — see
  [ARCHIVE_FORMAT.md](ARCHIVE_FORMAT.md#each-disc-is-converted-to-its-own-iso-ready-to-burn).
  This closed a real gap: previously, picking a disc-size media type produced
  correctly-sized `DISC_NN\` folders but no ISO, and Burn Disc only ever
  accepts an `.iso` — so a multi-disc archive had no in-app path to actually
  get burned. `Custom`/`Usb`/`ExternalDrive` media types are unaffected (they
  plan capacity for non-optical destinations, so there's nothing to burn).

## Disc burning (Phase 7 — implemented)

- The only first-party Windows burning API is **IMAPI2**
  (`imapi2.dll`/`imapi2fs.dll`), a COM Automation (IDispatch-based) API that
  was never re-wrapped for .NET Core/5+/8 — there is no in-box managed
  wrapper. `.NET` 8's source-generated COM interop (`[GeneratedComInterface]`)
  targets plain `IUnknown` interfaces and doesn't cleanly cover IMAPI2's
  Automation surface, so `Imapi2DiscBurner` instead uses **late-bound
  ("dynamic") COM interop** — `Type.GetTypeFromProgID` +
  `Activator.CreateInstance`, then calling members through C#'s `dynamic` —
  the same fundamental approach long-standing PowerShell/VBScript IMAPI2
  burning scripts use. This was the pragmatic choice given hand-authoring a
  COM-ABI-matching strongly-typed interface for this large an Automation
  surface, with no way to test-compile against the real typelib, carried more
  risk of a silent mismatch than late binding does.
- **Burning only accepts an already-built ISO** (see Phase 6 above) via a
  read-only `System.Runtime.InteropServices.ComTypes.IStream` wrapper
  (`ComStreamWrapper`) feeding `MsftDiscFormat2Data.Write()`. This app does
  not master a disc image directly from a folder — it never links against
  `IMAPI2FS`'s `MsftFileSystemImage` at all, keeping the COM surface much
  smaller than a full "burn this folder" feature would need.
- **Confirmed against real hardware** (a USB Blu-ray burner, BD-R media):
  drive enumeration (`MsftDiscMaster2` → `MsftDiscRecorder2`, cross-referenced
  with `Win32_CDROMDrive` over WMI for media-loaded state), `CloseTray()`, and
  burning a real ISO to a real disc all worked correctly, and the burned data
  was independently confirmed via a plain file-system read of the mounted
  drive letter — not just a "success" return value from IMAPI2.
- **This app only burns to blank media.** Testing surfaced a real, sharp
  edge: `IDiscFormat2Data.IsCurrentMediaSupported` only checks media *type*
  compatibility, not whether the disc already has data on it — writing a
  second time onto already-recorded media (without an explicit erase first)
  was confirmed to silently produce an unreadable disc rather than fail
  cleanly, which matches how write-once media (BD-R/DVD-R/CD-R) behaves in
  any burning software once a session is closed. `Imapi2DiscBurner` now
  checks `IDiscFormat2Data.MediaHeuristicallyBlank` before writing and
  refuses non-blank media outright with a clear message, rather than risk
  repeating that. Erasing already-recorded rewritable media (BD-RE/DVD±RW/
  CD-RW) via `MsftDiscFormat2Erase` is **not implemented** — a quick pass at
  it during testing found `IsCurrentMediaSupported` on the eraser object
  returned `false` for the specific BD-R media tested (consistent with BD-R
  being write-once and therefore not erasable at all), and getting erase
  right for every rewritable format is more hardware-specific nuance than
  was safe to guess at blindly. Reusing rewritable media currently means
  erasing it with another tool first.
- IMAPI2's write-progress events are exposed only via a COM connection-point
  (`IDiscFormat2DataEvents`), which late-bound `dynamic` interop cannot
  subscribe to. `BurnIsoAsync`'s progress is therefore coarse — 0.0 at start,
  1.0 on completion — with no incremental progress in between, and
  cancellation is only honored before the burn starts, not once `Write()` is
  underway.
- **Windows can take a few seconds to remount a disc's new session after
  `Write()` returns.** Confirmed against real hardware: reading the disc
  back immediately after a successful burn saw a false verification failure
  (the volume was briefly unreadable, not actually corrupted). `BurnIsoAsync`
  now polls the newly-written volume until it's listable (up to 20 seconds)
  before returning success, so a caller that immediately re-reads the disc —
  such as the auto-verify step in Burn Disc — doesn't race this and see a
  false failure.
- **BDXL (BD-100/128, UDF 2.60) is not reliably supported by IMAPI2** — this
  is a confirmed gap, not a hypothetical one; standard Windows tooling does
  not carry UDF 2.60 support. BDXL is documented as "build the UDF image
  yourself, then burn it with a third-party tool of your choice" — never
  promised as working end-to-end through this app.
- All COM interop and burning logic is isolated inside
  `PcGamePreservationStudio.Burning`. `IDiscBurner` in `Core` stays free of
  COM/process types (progress and capability are reported via plain DTOs and
  enums).
- **"Verify Disc"** (replacing its earlier "Coming Soon" placeholder) doesn't
  need new verification logic — a burned disc's mounted drive letter is just
  another folder path, so it reuses `IArchiveVerificationService.VerifyAsync`
  directly. The app will never claim a disc is verified merely because the
  burn process reported success — verification always requires an
  independent read-back pass against the recorded checksums, and correctly
  reports `Incomplete` (not a false `Verified`) for any disc that isn't a
  checksummed archive in the first place.

## Sequencing

1. Folder-only archive (no external dependency). ✅
2. ISO creation via oscdimg detection (external tool, no COM risk). ✅
3. IMAPI2 burning to blank media, confirmed against real BD-R hardware. ✅
4. Erasing already-recorded rewritable media (BD-RE/DVD±RW/CD-RW) — not
   implemented; use another tool to erase such media before reusing it here.
5. BDXL explicitly deferred / documented as third-party-tool territory.

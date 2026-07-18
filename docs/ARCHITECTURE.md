# Architecture

## Goals

- The UI (`PcGamePreservationStudio.App`) never depends directly on Steam,
  GOG, or burning-tool implementations — only on interfaces defined in
  `PcGamePreservationStudio.Core`.
- `Core` has zero platform/IO/COM/database dependencies. It is pure domain
  models and interfaces.
- Risky, external-tool-dependent code (disc burning COM interop, oscdimg
  process invocation) is isolated in its own project so it can fail, be
  mocked, or be replaced without destabilizing the rest of the app.

## Solution layout

```
src/
  PcGamePreservationStudio.App/              WPF shell, MVVM (CommunityToolkit.Mvvm),
                                              DI composition root, navigation
  PcGamePreservationStudio.Core/             Domain models + all service interfaces
  PcGamePreservationStudio.Platforms.Steam/  VDF/ACF parsing, library + registry detection
  PcGamePreservationStudio.Platforms.Gog/    installed-game + offline-installer detection
  PcGamePreservationStudio.Archiving/        file collection, save detection, checksums,
                                              metadata, folder-based archive building/verification,
                                              multi-disc splitting (via Media)
  PcGamePreservationStudio.Media/            disc capacity, safety margins, multi-disc file planning
  PcGamePreservationStudio.Burning/          oscdimg-backed ISO creation (Phase 6); IMAPI2 disc
                                              burning via late-bound COM interop (Phase 7)
  PcGamePreservationStudio.Analysis/         DRM/launcher evidence scanning (file-name markers only;
                                              never inspects or executes game binaries)
  PcGamePreservationStudio.Persistence/      SQLite archive catalog + settings
  PcGamePreservationStudio.Infrastructure/   logging setup, safe process execution,
                                              file-system helpers
tests/
  mirrors each src/ project, plus an Integration.Tests project
```

Only the projects needed for the current phase are added to the solution.
Later projects (`Burning`) are created when their phase actually begins.

## Service interfaces (defined in Core)

| Interface | Purpose | Status |
|---|---|---|
| `IGamePlatformProvider` | Enumerate games visible from one source (Steam, GOG, local folder) | Steam + GOG implemented (Phase 2, 5) |
| `IGameDetectionService` | Aggregate all `IGamePlatformProvider`s into one library | Implemented (Phase 2) |
| `IGogLibraryProvider` | GOG installed games + offline-installer grouping | Implemented (Phase 5) |
| `ISaveDetectionService` | Find likely save/config locations | Implemented (Phase 3) |
| `IDrmAnalysisService` | Report DRM/launcher evidence (never certainty) | Implemented (`PcGamePreservationStudio.Analysis`) |
| `IArchiveBuilder` | Collect, hash, package a folder or multi-disc archive | Implemented (Phase 3-4) |
| `IDiscCapacityService` | Media capacity / safety-margin / multi-disc planning | Implemented (Phase 4) |
| `IIsoBuilder` | Build a UDF ISO image | Implemented (Phase 6, oscdimg-backed) |
| `IDiscBurner` | Burn a prepared ISO to optical media | Implemented (Phase 7, IMAPI2-backed) |
| `IArchiveVerificationService` | Verify an archive/burned disc against its checksums | Folder + multi-disc + burned-disc verification implemented (Phase 3-4, 7); reading checksums from inside a .iso file directly is not |
| `IArtworkService` | Manage cover art / printable archive sheets | Not yet implemented |
| `ISettingsService` | Load/save `AppSettings` | Implemented (Phase 1) |
| `IArchiveCatalogRepository` | Persist the archive catalog | Implemented (Phase 1) |
| `IRestoreService` | Restore a flat archive back to disk (verify-then-copy) | Implemented (Phase 8) |

Interfaces for unimplemented phases are still declared now so the DI
composition root and navigation shell don't need rework later; they're
registered with stub implementations that surface "Coming Soon" rather than
throwing on use.

## Phased roadmap

1. **App shell** — WPF navigation, DI, logging, settings, mock game library, catalog. ✅
2. **Steam detection** — libraries, installed games, manifest parsing, game detail. ✅
3. **Archive builder** — file collection, save-location detection, SHA-256, metadata,
   folder-only archive, verification, wired into a Create Archive flow. ✅
4. **Media planning** — CD/DVD/BD advertised vs. usable capacity, configurable safety
   margins, first-fit-decreasing multi-disc file placement (never splits a file), archives
   split into DISC_01\, DISC_02\, ... with per-disc checksums and a disc map, wired into
   a media-type selector with a live capacity preview. ✅
5. **GOG support** — installed detection (registry-based), offline-installer grouping
   (base game/DLC/patch/soundtrack/manual/extra by filename heuristic), installer-only
   archives, GOG-specific preservation ratings, a "Create Archive from GOG Installers"
   flow. ✅
6. **ISO creation** — `oscdimg.exe` detection (ADK standard paths, PATH, Settings override),
   an `IsoOnly` media type that builds the usual folder archive then converts it into a UDF
   ISO (`-u2`), falling back to keeping the folder intact with a clear warning if oscdimg isn't
   found or the build fails (see [BURNING_BACKENDS.md](BURNING_BACKENDS.md)). ✅
7. **Disc burning** — IMAPI2 via late-bound COM interop (`MsftDiscMaster2`/`MsftDiscRecorder2`/
   `MsftDiscFormat2Data`), confirmed against real BD-R hardware; burns only to blank media
   (writing over already-recorded media was found to corrupt it, not fail cleanly — see
   [BURNING_BACKENDS.md](BURNING_BACKENDS.md)); a "Verify Disc" page reuses
   `IArchiveVerificationService` against the disc's mounted drive letter. ✅
8. **Restore workflow** — reads Metadata\archive_manifest.json and Metadata\save_locations.json,
   always re-verifies the archive against its checksums before touching anything (refuses to
   proceed on a failed/incomplete verification), then copies Game\/Installers\ to a chosen install
   destination, Platform\ files to a PlatformFiles\ subfolder there (with a note that the user must
   move them into their platform library's own structure themselves — this app never edits a Steam/
   GOG library), and each selected Saves\ folder to its recorded (or user-redirected) original path.
   Files that already exist at their destination are left alone unless overwrite is requested.
   Scoped to flat, single-root archives — a multi-disc archive's DISC_NN\ folders are not stitched
   back together automatically. Reachable from the Restore page or a "Restore…" button on each
   Archives catalog entry. ✅
9. Packaging — artwork, printable archive sheet.

Each phase is proposed and approved before implementation starts.

## Key technical risks

See [BURNING_BACKENDS.md](BURNING_BACKENDS.md) for the disc-burning/UDF risk
assessment (the highest-risk area of this project). Other notable risks:

- **Long paths**: game trees can exceed `MAX_PATH`. `app.manifest` declares
  `longPathAware`, but file-walking code still defensively handles
  `PathTooLongException` since the OS-level policy isn't guaranteed enabled.
- **High-DPI**: `app.manifest` declares `PerMonitorV2` DPI awareness.
- **x64-only**: no ARM64 Windows support without emulation, stated explicitly
  rather than assumed.
- **Process execution**: any shell-out uses `ProcessStartInfo.ArgumentList`,
  never shell string concatenation.

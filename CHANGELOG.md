# Changelog

All notable changes to this project are documented in this file.

## [Unreleased]

### Added
- Phase 1: WPF application shell, dependency injection, structured logging,
  settings persistence, first-run experience, mock game library, archive
  catalog (SQLite-backed, empty).
- Phase 2: Steam library detection (`libraryfolders.vdf`, registry, standard
  paths), `appmanifest_*.acf` parsing, game detail view, manual local-folder
  source.
- Phase 3: Save-location detection (Documents\My Games, AppData, Saved
  Games, Steam Cloud userdata), folder-based archive builder with SHA-256
  checksums and metadata generation, archive verification (re-hash and
  compare), preservation rating, and a Create Archive flow wired into the
  game detail page.
- Phase 4: CD/DVD/BD advertised vs. usable capacity calculations with
  configurable safety margins, first-fit-decreasing multi-disc file
  planning (never splits a single file), archive splitting into
  `DISC_01\`, `DISC_02\`, ... folders with per-disc checksums and a disc
  map, multi-disc-aware verification, and a media-type selector with a
  live capacity preview in the Create Archive flow.
- Phase 5: GOG registry-based installed-game detection
  (`HKLM\SOFTWARE\WOW6432Node\GOG.com\Games\<gameID>`), GOG offline
  installer grouping by filename (base game/DLC/patch/soundtrack/manual/
  extra), installer-only archive building (no installed-game folder
  required), GOG-specific preservation ratings (Excellent for offline
  installers, Good for installed-game copies), and a "Create Archive from
  GOG Installers" flow in the UI.
- Phase 6: `oscdimg.exe` detection (Windows ADK standard paths, `PATH`, and
  a Settings override), an "ISO Image (.iso)" media type that converts a
  built folder archive into a UDF ISO, and a safe fallback (keep the folder,
  show a clear warning) when oscdimg isn't found or the build fails. This
  project never bundles `oscdimg.exe` itself.
- Phase 7: IMAPI2-backed disc burning via late-bound COM interop
  (`MsftDiscMaster2`/`MsftDiscRecorder2`/`MsftDiscFormat2Data`), a "Burn
  Disc" page (pick an ISO and a detected drive, burn, then auto-verify by
  reading the disc back), and a "Verify Disc" page that reuses
  `IArchiveVerificationService` against any drive's mounted volume. Confirmed
  against real Blu-ray burner hardware. Refuses to burn onto already-recorded
  media (writing a second time onto non-blank media was found, via that
  hardware testing, to silently corrupt the disc rather than fail cleanly).
- Phase 8: `IRestoreService`, reading `Metadata\archive_manifest.json` and
  `Metadata\save_locations.json` and always re-verifying an archive's
  checksums before restoring anything (refusing to proceed on failure). A
  Restore page (also reachable via "Restore…" on Archives entries) copies
  `Game\`/`Installers\` to a chosen destination, `Saves\<name>\` folders back
  to their recorded or redirected paths, and `Platform\` files to a labeled
  subfolder for the user to place themselves — restoring never edits a
  Steam/GOG library or grants ownership. Existing destination files are
  skipped unless overwrite is requested.
- `IDrmAnalysisService` (`PcGamePreservationStudio.Analysis`): scans a game's
  install folder (root + immediate subfolders) for known launcher/DRM marker
  files — Steamworks SDK, Ubisoft Connect, EA Desktop/Origin, Epic Online
  Services, legacy Games for Windows Live, Rockstar Games Social Club,
  Battle.net — and reports a confidence-qualified label plus the specific
  evidence found. Never opens, executes, or disassembles any executable, and
  never asserts certainty (e.g. "likely" and "may be required" labels, not
  guarantees). Wired into the Game Detail page's "Preservation analysis"
  card, replacing the previous static placeholder text.
- App branding: custom logo integrated as the application icon (title bar,
  taskbar) and shown in the nav rail and About page.
- Multi-disc archives targeting a physical optical medium (CD-R, DVD-5/9,
  BD-25/50/100/128) now have each `DISC_NN\` folder automatically converted
  into its own `DISC_NN.iso` via the same oscdimg-backed builder used for the
  single-disc "ISO Image (.iso)" destination — closing a real gap where
  picking a disc-size media type produced correctly-sized folders but no way
  to actually burn them (Burn Disc only ever accepts an `.iso`). A disc whose
  ISO build fails keeps its folder as a fallback while every other disc still
  gets its ISO; `Custom`/`Usb`/`ExternalDrive` media types are unaffected.

### Fixed
- `FirstRunWindow.xaml`'s `Window.Icon` used a relative path
  (`Assets/AppIcon.ico`), which WPF resolves relative to the XAML file's own
  folder rather than the project root — since the window lives in `Views/`,
  this resolved to a nonexistent path and threw an unhandled
  `XamlParseException` on every startup. Fixed by using an absolute
  pack-style path (`/Assets/AppIcon.ico`).
- `oscdimg` was silently omitting hidden files from every built ISO — for
  GOG games this meant `goggame-<id>.hashdb`/`.info`/`.script` were always
  missing, despite being listed in the archive's own checksums manifest.
  Found via a real burn-and-verify cycle against real hardware, which is
  exactly the kind of defect independent read-back verification exists to
  catch. Fixed by adding oscdimg's `-h` flag.
- `Imapi2DiscBurner` could report a real, successful burn as a false
  verification failure if the disc was read back immediately — Windows
  needs a few seconds to remount a newly-written disc's session. Fixed by
  polling the volume until it's listable before `BurnIsoAsync` returns.

# Archive Format

**Status: `Game\`, `Installers\`, `Platform\`, `Saves\`, `Metadata\`,
`Checksums\`, `Restore\`, the root `README.txt`, and multi-disc splitting
(`DISC_01\`, `DISC_02\`, ...) are all implemented (Phase 3-5).**
`Configuration\`, `Registry\`, `Artwork\`, `Manuals\`, and `Extras\` are not
populated yet — they land with packaging (Phase 9). Multipart 7-Zip
compression for files too large for a single disc is not implemented (see
"Files too large for any disc" below).

## Folder layout (folder-only / single-disc destinations)

```
GAME_TITLE_ARCHIVE\
  Game\            Copied game files                                    Implemented
  Installers\       GOG offline installer files, if any                 Implemented
  Platform\         Steam/GOG manifest files                            Implemented
  Saves\            Archived save files                                 Implemented
  Configuration\    Archived config files                               Not yet implemented
  Registry\         Exported game-specific registry keys (opt-in, scoped) Not yet implemented
  Artwork\          Cover art, disc labels                              Not yet implemented
  Manuals\          PDFs/manuals                                        Not yet implemented
  Extras\           Soundtracks, bonus content                          Not yet implemented
  Metadata\         game_info.json, game_info.txt, preservation_report.json,
                    preservation_report.txt, save_locations.json, source_files.json,
                    archive_manifest.json, build_log.txt                Implemented
  Checksums\        SHA256SUMS.txt, verification_manifest.json          Implemented
  Restore\          README.txt (restore instructions)                  Implemented
  README.txt                                                            Implemented
```

Not every folder is populated for every archive — e.g. `Saves\` only appears
when the user included at least one detected save location, and an
installer-only archive (built from the "Create Archive from GOG Installers"
flow, with no installed-game folder involved) populates `Installers\` but
never creates `Game\` at all.
`Metadata\archive_manifest.json` is the authoritative record of what an
archive contains and how to restore it; the restore workflow (Phase 8) reads
it (and `Metadata\save_locations.json`, via each entry's `archiveFolderName`
field naming its `Saves\<name>\` folder) and always re-verifies checksums
before touching anything — it refuses to restore past a failed or incomplete
verification.

Checksums use SHA-256, generated with streaming reads
(`PcGamePreservationStudio.Archiving.Sha256Hasher`). `Checksums\SHA256SUMS.txt`
uses the standard `<hash>  <relative-path>` format with forward-slash relative
paths (e.g. `Game/bin/data.pak`); `Checksums\verification_manifest.json` is
the same data structured for programmatic verification.

Verification (`IArchiveVerificationService`) always re-reads and re-hashes
every file listed in `SHA256SUMS.txt` (or, for a multi-disc archive, every
`DISC_NN\Checksums\SHA256SUMS.txt`) — a successful build is never treated as
proof of integrity on its own. It reports `Verified`, `VerifiedWithWarnings`
(unexpected extra files present), `Failed` (missing, modified, or unreadable
files), or `Incomplete` (no checksums file found anywhere).

## Multi-disc archives

When the destination is a disc-based medium (CD-R, DVD-5/9, BD-25/50/100/128,
or a custom capacity), `PcGamePreservationStudio.Media.DiscCapacityService`
plans file placement with a first-fit-decreasing bin packing: files are
sorted largest-first and each placed on the first disc with room, so **a
single file is never split across discs**. The archive is then reorganized
into:

```
GAME_TITLE_ARCHIVE\
  DISC_01\
    Game\, Platform\, Saves\   This disc's share of the content
    Checksums\SHA256SUMS.txt   Scoped to just this disc's files
    Metadata\disc_manifest.json   { gameTitle, discNumber, discCount, mediaType, fileCount, totalBytes }
  DISC_02\
    ...
  Metadata\                    Unchanged: describes the whole archive once
    disc_map.json              { mediaType, discCount, safetyMarginBytes, discs: [...], filesTooLargeForMedium: [...] }
  README.txt, Restore\README.txt   Unchanged, describe the whole archive
```

The flat `Game\`/`Platform\`/`Saves\`/`Checksums\` used for folder-only
archives are removed once splitting completes (files are moved, not copied,
into their `DISC_NN\` folder) — a stale root-level checksums file that still
pointed at now-moved files would be actively misleading.

### Each disc is converted to its own ISO, ready to burn

For an actual physical optical-disc medium (CD-R, DVD-5/9, or BD-25/50/100/128),
`ArchiveBuilder` immediately converts each `DISC_NN\` folder into `DISC_NN.iso`
via `IIsoBuilder` (the same oscdimg-backed builder used for the single-disc
"ISO Image (.iso)" destination — see [`BURNING_BACKENDS.md`](BURNING_BACKENDS.md)),
and deletes the folder once its ISO is confirmed on disk. The end result is:

```
GAME_TITLE_ARCHIVE\
  DISC_01.iso
  DISC_02.iso
  Metadata\disc_map.json, build_log.txt, ...
  README.txt, Restore\README.txt
```

Each `.iso` is immediately usable with Burn Disc — no manual "convert this
folder to an ISO" step is needed. If a specific disc's ISO build fails
(backend not found, or the build itself fails), that disc's `DISC_NN\` folder
is kept intact as a fallback (`ArchiveBuildResult.IsoBuildWarning` explains
which disc and why) while every other disc still gets its ISO — a single
failure doesn't lose the whole archive.

This conversion only applies to genuine optical-disc media types. Selecting
`Custom`, `Usb`, or `ExternalDrive` (capacity-based planning for a non-optical
destination) still leaves plain `DISC_NN\` folders, since there's nothing to
burn them to.

### Files too large for any single disc

A file larger than one disc's usable capacity minus its safety margin cannot
be placed at all. `ArchiveBuildResult.FilesTooLargeForMedium` lists these —
they're left in place, not silently dropped, and the UI surfaces a warning
recommending a larger medium or a folder-only destination. Multipart 7-Zip
compression (splitting a single large file across parts, only if 7-Zip is
already installed and the user explicitly enables it — never downloaded
automatically) is not implemented yet.

## ISO output (`IsoOnly` media type, Phase 6)

Selecting the ISO Image media type builds the normal folder-based archive
above, then converts that staged folder into a single UDF ISO via a detected
`oscdimg.exe` (see [BURNING_BACKENDS.md](BURNING_BACKENDS.md)):

- **On success**, the staged `GAME_TITLE_ARCHIVE\` folder is deleted and
  `GAME_TITLE_ARCHIVE.iso` (a sibling of where the folder was) is the only
  remaining output. `ArchiveBuildResult.IsoPath` points at it.
- **If oscdimg isn't found, or the build otherwise fails**, the staged
  folder is left in place exactly as a folder-only archive would be, and
  `ArchiveBuildResult.IsoBuildWarning` explains why — a failed ISO build
  never costs the user their archive.
- The `Metadata\build_log.txt` line for "Archive build completed" is written
  before the ISO step (not after, unlike every other media type), since it
  needs to already be inside the folder if that folder becomes the ISO's
  contents.
- ISO-level verification (re-hashing files from inside a built ISO) is not
  implemented yet; when the ISO build fails and the folder is kept instead,
  that folder is verified normally.

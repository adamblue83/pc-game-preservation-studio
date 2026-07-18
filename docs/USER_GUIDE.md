# User Guide

A walkthrough of PC Game Preservation Studio from first launch through
creating, verifying, and restoring an archive — written from a new user's
perspective.

## Prerequisites

- **Windows 10 or 11, x64.** No ARM64 support.
- **.NET 8 Desktop Runtime** to run the app (the .NET 8 SDK if you're
  building it yourself).
- **Windows ADK — Deployment Tools component (optional).** Only needed if
  you want to use the **ISO Image (.iso)** destination in Create Archive.
  Without it, ISO builds fall back to a complete, verified folder archive
  with a message explaining what's missing — nothing is lost, you just
  won't get a `.iso` file until it's installed. Get it from
  [the official Windows ADK page](https://learn.microsoft.com/windows-hardware/get-started/adk-install)
  — select only the **Deployment Tools** feature; you don't need the rest of
  the ADK. This app never bundles or redistributes `oscdimg.exe` itself.
- **A writable optical drive (optional).** Only needed for the Burn Disc /
  Verify Disc pages. Everything else works with just a folder on disk.

## 1. First launch

The first time you start the app, a welcome screen explains plainly what it
does and doesn't do — it never bypasses DRM, emulates a platform, or removes
ownership checks — before it touches anything. It also tells you it's about
to look for a Steam installation using standard paths and the registry; no
Steam username, password, or login is ever requested. There's a checkbox to
skip all platform auto-detection if you'd rather add folders manually.

## 2. The Library

After continuing past the welcome screen, the Library populates
automatically from any detected Steam installation (and GOG, if installed).
No login, no manual setup — it just reads the same library files Steam
itself already wrote to disk. Games can also be added manually via **Add
Local Folder…** or **Add GOG Offline Installer…**.

Click any game to see its detail page: platform, App ID, install folder, the
platform manifest file it was read from, and installed size — all read
directly from files Steam/GOG already had on disk.

## 3. Creating your first archive

From a game's detail page, click **Archive…**. The Create Archive page:

1. **Save locations detected** — likely save/config folders the app found
   (e.g. under `Saved Games`, `AppData`, or a platform's cloud-save folder),
   each with a confidence level and the reason it was flagged. Review and
   uncheck anything you don't want included — these can contain personal
   files beyond just save data.
2. **Destination media** — `Folder Only` is the simplest choice for a first
   archive. Other options split the archive across CD/DVD/BD-sized chunks,
   build a UDF `.iso` (needs the Windows ADK — see Prerequisites), or (once
   you have a `.iso`) get burned via the Burn Disc page.
3. **Destination folder** — pick anywhere with enough free space, via the
   normal folder browser.

Click **Archive Now**. The result panel reports success, file/byte counts,
a **preservation rating** (`Excellent`/`Good`/`Unknown` with the reasons
behind it — e.g. whether Steam ownership is still required to reinstall),
and a **verification** result from an independent re-hash of every copied
file — a build is never reported as trustworthy just because the copy step
didn't error.

## 4. Archives

The Archives page lists every archive you've built, with its status and a
one-click **Restore…** shortcut that jumps straight to the Restore page with
that archive pre-selected.

## 5. Restoring an archive

Restore (from the left nav, or via an Archives entry) always re-verifies
every file against its recorded checksums *before* touching anything, and
refuses outright if that check fails — nothing is ever restored from a
corrupted or incomplete archive. Once verified, it shows:

- The archive's title, platform, App ID, and preservation rating.
- What it contains (game files, offline installers, platform files, save
  locations).
- A destination folder for game/installer files, with an option to
  overwrite existing files there (files that already exist are otherwise
  left alone and listed, not overwritten).
- Each save location, pre-filled with its original recorded path — editable
  if you'd rather restore it somewhere else.

Platform files (e.g. a Steam manifest) are copied to a clearly labeled
`PlatformFiles\` subfolder rather than directly into a Steam library — the
app tells you to move them yourself if you want Steam to recognize the
install, since restoring files never grants or transfers ownership.

## 6. Optional: ISO, burning, and disc verification

- **ISO Image** as a Create Archive destination converts the built archive
  into a single `.iso` (requires the Windows ADK — see Prerequisites).
- **Burn Disc** burns an existing `.iso` to a detected, blank optical drive,
  then automatically reads the disc back and verifies it. Only blank media
  is accepted — writing onto already-recorded media isn't supported and is
  refused up front.
- **Verify Disc** re-checks any inserted disc (burned by this app) against
  its recorded checksums, independent of any burn step.

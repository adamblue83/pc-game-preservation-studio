# Platform Support

## Steam (Phase 2 — implemented)

Detected via, in order:

1. Standard install paths.
2. Windows registry (`HKCU\Software\Valve\Steam`, `InstallPath`).
3. A user-selected folder in Settings.

Once the Steam install path is known:

- `steamapps\libraryfolders.vdf` is parsed (via `Gameloop.Vdf`) to discover
  every Steam library on the machine, including secondary drives.
- Each library's `steamapps\appmanifest_*.acf` files are parsed for app ID,
  name, install directory, size, and (when present) build ID.
- No Steam username, password, or authentication token is ever requested,
  read, or stored. This project does not automate Steam login.
- For this version, Steam support only archives **locally installed files**.
  A future, clearly-separated integration may open the official Steam
  client or store page, but will never impersonate or replace Steam
  authentication.

## GOG (Phase 5 — implemented)

Two workflows are supported:

- **Installed GOG games.** Detected by reading each game's registry key under
  `HKLM\SOFTWARE\WOW6432Node\GOG.com\Games\<gameID>` (falling back to
  `SOFTWARE\GOG.com\Games` on 32-bit installs), which GOG Galaxy writes for
  every installed title. No Galaxy process, account, or session token is
  read — only the registry values GOG itself already wrote (game name, ID,
  install path, executable). Archiving an installed GOG game copies its
  install folder the same way Steam games are copied, and is rated **Good**
  (Galaxy or an internet connection may still be needed to reinstall/verify).
- **Official GOG offline installers.** Point the "Create Archive from GOG
  Installers" flow at a folder containing GOG's official downloaded
  installer files (`setup_*.exe`, numbered `.bin` parts, and separate DLC/
  patch/soundtrack/manual installers). Files are grouped by shared base
  filename and classified by keyword (`dlc`, `patch`/`update`,
  `soundtrack`/`ost`, `manual`) or file type, with every group shown to the
  user — including which files it contains — before archiving. This
  workflow needs no installed game, no registry entry, and no GOG Galaxy at
  all, so it also works for games no longer installed. Archives built this
  way are rated **Excellent**, since GOG's offline installers are DRM-free
  and self-contained by design.

No GOG account login or automated downloading is implemented or planned;
both workflows only read installer files or registry values the user (or
GOG Galaxy, on their own machine) already produced.

## Local folders (Phase 1/2 — implemented)

Any folder can be manually added as a source, tagged as installed game,
offline installer, portable game, mod, patch, DLC, personal project, or
other.

## What this app will never do

- Bypass DRM or remove ownership checks.
- Patch, unpack, or otherwise modify a game's executables or DLLs.
- Emulate Steam, GOG Galaxy, or any other platform's authentication.
- Collect platform passwords, session cookies, or auth tokens.
- Claim with certainty that a restored game will run offline — DRM/launcher
  findings are always reported as evidence, never as guarantees.

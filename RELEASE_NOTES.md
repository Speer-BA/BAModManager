# Broken Arrow Mod Manager v1.1.0 · Mod Launcher v1.1.0

**Now two programs:** the full **Mod Manager** for modders, and a new one-button **Mod Launcher** for players.

## Downloads

| File | For |
|---|---|
| `BAModManager-v1.1.0.zip` | Modders - the full Mod Manager |
| `BAModLauncher-v1.1.0.zip` | Modders who want to give players a launcher for **their** modpack (set it up once, share the folder) |
| `ActualArrow-Launcher-v1.1.0.zip` | ActualArrow players - unzip, press PLAY |

## Mod Launcher (new)
- **One PLAY button** - installs and updates MelonLoader and the modpack from GitHub, then starts the game with Anti-Cheat disabled.
- **Loading screen** instead of MelonLoader's console window.
- **Play vanilla with no mods** - removes `version.dll` and starts the normal game with Anti-Cheat. PLAY turns mods back on, even offline.
- **Stable / Testing** - a modpack can offer a testing version (a second GitHub repo); players switch on the modpack card.
- **Locked to one modpack** - the modder sets it up once (*Set up modpack (for modders)…* → `launcher.json`); players can't change it or install other mods with it.
- **Settings:** language, game folder, *Uninstall mod* (game back to normal, launcher stays), uninstall the launcher.
- PLAY is locked while the game is running.

## Mod Manager v1.1.0
- **Modpacks** are their own type: listed first, *Modpacks* filter, only one on at a time.
- *Create package…* can mark a package as a modpack and save its GitHub repo for updates.
- PLAY is locked while the game is running.

## Both
- Fewer antivirus false alarms: no DLLs unpacked into Temp on start, no background process polling, and uninstall no longer deletes itself through a hidden command (it opens the folder so you delete the exe).
- Proper file details (version, description) in the exe.
- **The download is now a folder** - the exe with a few DLLs next to it. Keep them together.

### Updating
Unzip over your old folder. Your mods and settings stay.

> [!WARNING]
> **DO NOT play Broken Arrow with Anti-Cheat turned on while MelonLoader is installed.** PLAY always starts the game with Anti-Cheat disabled; for regular online matches use **Play vanilla with no mods** (Mod Launcher) or uninstall MelonLoader (Mod Manager).

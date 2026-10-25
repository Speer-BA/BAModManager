<div align="center">

<img src="Assets/logo.png" width="96" alt="Broken Arrow Mod Manager logo">

# Broken Arrow Mod Manager

**Install MelonLoader and mods for Broken Arrow in a couple of clicks - and remove them just as easily.**
**Plus a one-button Mod Launcher that gets players into a modpack without any setup.**

[**⬇ Download the latest release**](../../releases/latest) · Windows 10/11 · No .NET needed

English · Deutsch · Français · Русский · 中文

</div>

<!-- Add a screenshot: put it in docs/screenshot.png and remove the comment marks around the next line.
<p align="center"><img src="docs/screenshot.png" alt="Mod Manager window"></p>
-->

---

> [!WARNING]
> **DO NOT play Broken Arrow with Anti-Cheat turned on while MelonLoader is installed.**
> Anti-Cheat can detect MelonLoader and mods, and that can get your account banned. With MelonLoader installed, only play with the Anti-Cheat disabled option (Steam: right click on Broken Arrow → Properties → General → Selected Launch Option → *Play Anti-Cheat Disabled*).
> For normal online matches (quick match, ranked, skirmish), uninstall MelonLoader first (untick *Also delete all mods* to keep your mods) and select *Play Broken Arrow* in the Steam launch option.

## Two programs

| | **Mod Launcher** (`BAModLauncher.exe`) | **Mod Manager** (`BAModManager.exe`) |
|---|---|---|
| For | Players | Modders and tinkerers |
| What it does | One **PLAY** button for **one modpack** - set up by the modder, players can't change it | Everything: install, switch and remove any mods, MelonLoader, create `.bamod` packages |
| Settings | Language, game folder, uninstall mod, uninstall launcher | All of them |

### Mod Launcher - for players

- **▶ PLAY** - installs MelonLoader and the modpack if they're missing, updates them when there's a new version on GitHub, and starts Broken Arrow **with Anti-Cheat disabled**. MelonLoader's console window is hidden; a loading screen shows what's happening instead (the first start after installing or updating takes a few minutes).
- **Play vanilla with no mods** - removes MelonLoader's `version.dll` from the game folder (a copy is kept, so PLAY turns mods back on instantly, even offline) and starts the normal game **with Anti-Cheat**.
- **⚙ Settings** - language, game folder, *Uninstall mod* (removes the modpack, MelonLoader and all mods; PLAY installs them again) and uninstalling the launcher. Nothing there changes the modpack.
- The modpack's own `.bamod` can be dropped on the launcher (for players who download it by hand). Any other mod is turned away - that's what the Mod Manager is for.

### Giving players a launcher for your modpack

**Option A - no building needed:**

1. Download `BAModLauncher-vX.Y.Z.zip`, unzip it and run `BAModLauncher.exe`.
2. Click **Set up modpack (for modders)…** and paste your modpack's GitHub link. The launcher checks that its latest release has a `.bamod` attached.
3. A `launcher.json` file is written next to the exe:
   ```json
   { "modpack": "Speer-BA/ActualArrow" }
   ```
4. Zip the **whole launcher folder** (`BAModLauncher.exe`, the DLLs next to it and `launcher.json`) and share that. Players unzip and press PLAY.

Players can't change the modpack - the setup button is gone once `launcher.json` exists. To change it yourself, delete `launcher.json`.

**Stable and Testing.** A launcher can offer two versions of the modpack. Players switch with the *Stable / Testing* buttons on the modpack card (Testing asks for confirmation); the other version is downloaded on the next PLAY. Add the second repo:

- in **Set up modpack** (*Testing version - GitHub link*), which writes `{ "modpack": "owner/repo", "testing": "owner/repo-testing" }` to `launcher.json`, or
- as the second argument of `build_launcher.bat owner/repo owner/repo-testing`.

Both repos publish releases the same way (a `.bamod` attached). Without a testing repo the buttons don't appear.

**Option B - built in:** `build_launcher.bat owner/repo` builds a launcher with the modpack baked into the exe (no `launcher.json` needed), e.g. `build_actualarrow.bat`.

## Modpacks

A **modpack** is a `.bamod` whose `mod.json` says `"type": "modpack"` and names its GitHub repo:

```json
{
  "id": "speer.actualarrow",
  "name": "ActualArrow",
  "version": "1.0.5",
  "type": "modpack",
  "repo": "Speer-BA/ActualArrow"
}
```

- **Updates.** On PLAY the launcher looks at the latest release of the modpack's repo. If its tag (`v1.0.5`) is newer than the installed version, the `.bamod` attached to that release is downloaded and installed.
- **Publishing an update:** bump `version` in `mod.json`, create a GitHub release with the same version as its tag, attach the `.bamod`. Keep the `id` the same forever.
- **In the Mod Manager**, modpacks are listed first, with the type *Modpack* and their own *Modpacks* filter. Only one modpack is on at a time - switching one on switches the other off. *Create package…* has a *This is a modpack* option and a field for the repo.

## Features

- **One-click MelonLoader** - installs the latest release from GitHub, updates it, or removes it (your mods can stay).
- **Drag & drop mods** - `.bamod` packages, MelonLoader mods (`.dll`), models (`.bundle`) and `.zip` mod packs. Double-click a `.bamod` file to install it.
- **Switch mods on and off** without deleting anything, or remove them - files go to the Recycle Bin.
- **Mod details** - picture, description, version, author, website, read me and the full file list.
- **Requirements** - mods can depend on other mods. The manager warns about missing or outdated ones, offers to switch them on, and warns before you disable something another mod needs.
- **Finds the game automatically** through Steam and starts it with the **PLAY** button.
- **For modders** - a built-in tool that packs your mod into a `.bamod` file.
- **5 languages**, dark UI in the style of the game, and a clean uninstall.

## Installation

### Mod Launcher (players)

1. Download the launcher zip for your modpack (for ActualArrow: `ActualArrow-Launcher-vX.Y.Z.zip`) and unzip it anywhere.
2. Run `BAModLauncher.exe` and press **PLAY**. MelonLoader and the modpack install themselves; the first start takes a few minutes.

### Mod Manager (modders)

1. Download `BAModManager-vX.Y.Z.zip` from [Releases](../../releases/latest) and unzip it anywhere.
2. Run `BAModManager.exe`. No installer, no .NET to install - keep the DLLs next to the exe.
3. The game folder is found automatically. If not, set it on the **Settings** page.
4. Go to **MelonLoader → Install MelonLoader**. The first game launch afterwards takes a few minutes - that's normal.
5. Drag mods onto the window (or double-click a `.bamod` file) and press **PLAY**.

> [!NOTE]
> The exe isn't code-signed, so Windows SmartScreen may show *"Windows protected your PC"*. Click **More info → Run anyway**.
>
> Some antivirus programs (e.g. Kaspersky) can flag the launcher by its behaviour - it downloads MelonLoader and puts its `version.dll` into the game folder, which is how MelonLoader works. If that happens, add the launcher folder and the Broken Arrow folder to the antivirus exclusions. The source code is all here.

Close the game while you change mods. If the game is installed under `Program Files`, run the Mod Manager as administrator.

## Using it

| Page | What it's for |
|---|---|
| **Mods** | All your mods with an on/off switch, search, *All / Enabled / Disabled* filters and a details panel. Right-click a mod for more. |
| **MelonLoader** | Install, update or uninstall the loader; open `Latest.log` when a mod doesn't load. |
| **Settings** | Language, game folder, quick folder links, `.bamod` double-click, *Create mod package*, where each file type goes, uninstall. |
| **Activity log** | Everything the Mod Manager did in this session. |

**PLAY** starts the game through Steam. While MelonLoader is installed it always starts it **with Anti-Cheat disabled** (the same as Steam's *Play Anti-Cheat Disabled* option), so you can't launch a modded game with Anti-Cheat by accident.

**Keyboard:** Space - switch on/off · Delete - remove · F5 - refresh · Esc - deselect.

Installing a mod with the same id (or name) again **updates** it. Switching a mod off renames all of its files to `….disabled`, so the game and MelonLoader ignore them; switching it on renames them back. Mods you copied into the game folder by hand still show up (Source: *Manual*) and can be switched off or removed too.

## For modders: `.bamod` packages

A `.bamod` is one file with everything your mod needs: DLLs, models, a picture, a description and its requirements. Players double-click it (or drop it on the Mod Manager) to install, and it updates and uninstalls cleanly.

**The easy way:** Mods page → **Create package…** (or Settings → *For modders*). Pick your mod folder, fill in the form, choose a picture - done.

**By hand:** a `.bamod` is a renamed `.zip` that uses the same folders as the game folder:

```
CoolTanks-1.2.0.bamod
├─ mod.json          ← required: who/what the mod is
├─ preview.png       ← optional: square picture (256×256 or 512×512)
├─ README.md         ← optional: shown with "Read me"
├─ Mods/
│  └─ CoolTanks.dll
├─ UserLibs/         ← extra DLLs your mod needs (optional)
└─ UserData/
   └─ CoolTanks/
      └─ models/
         └─ tank.bundle
```

### mod.json

```json
{
  "id": "author.cooltanks",
  "name": "Cool Tanks",
  "version": "1.2.0",
  "author": "Author",
  "description": "Adds new tank models to Broken Arrow.",
  "gameVersion": "1.2.1",
  "website": "https://github.com/…",
  "requires": {
    "author.modelloader": ">=1.0",
    "someone.helperlib": { "version": "2.0", "url": "https://github.com/…/releases" }
  },
  "optional": { "author.iconpack": "*" },
  "incompatible": { "someone.othermodelmod": "*" }
}
```

| Field | |
|---|---|
| `id` | **Required.** Unique, lower-case, never changes: `author.modname`. Updates and requirements use it. |
| `name`, `version` | **Required.** Installing a higher version of the same `id` updates the mod. |
| `author`, `description`, `gameVersion`, `website` | Optional, shown in the details panel. |
| `requires` | Mods yours needs. Add a `url` and players get an *Open download page* button when it's missing. |
| `optional` | Mods yours can use if they're installed. |
| `incompatible` | Mods that break yours; players are warned if both are on. |
| `type` | `"modpack"` for a modpack (see *Modpacks* above). Leave it out for a normal mod. |
| `repo` | GitHub repo (`owner/name` or a github.com link) whose latest release is this mod's newest version. Required for modpacks. |

**Version rules:** `"*"` any · `"1.2"` 1.2 or newer · `">=1.2, <2.0"` · `"=1.2.3"` exactly · `"^1.2"` any 1.x from 1.2 · `"~1.2"` any 1.2.x.

A requirement on a mod that players installed by hand (no `mod.json`) still works if its DLL is named like the last part of the id - `author.modelloader` matches `ModelLoader.dll`.

For plain MelonLoader mods, the Mod Manager also reads the name, version, author and description from your DLL's `[MelonInfo]` and assembly attributes.

**Plain zips and loose files** still work: a `.zip` laid out as above (without `mod.json`), or single `.dll` / `.bundle` files. One wrapper folder inside a zip (`MyMod-1.2/Mods/...`) is fine. Files outside `Mods/`, `UserData/`, `Plugins/` and `UserLibs/` are placed by the rules in **Settings → Where files go**.

## Where things are stored

| What | Where |
|---|---|
| Mod Manager settings (language, game folder, file rules) | `%APPDATA%\BAModManager\settings.json` |
| Mod Launcher settings, and the parked `version.dll` while mods are off | `%APPDATA%\BAModLauncher\` |
| Which modpack a Mod Launcher is for | `launcher.json` next to `BAModLauncher.exe` |
| What was installed, mod pictures and read-mes | `<game folder>\UserData\BAModManager\` |

**Settings → Uninstall…** (in either program) removes that program, its settings and the `.bamod` file association. You choose whether MelonLoader and your mods stay (default) or go too.

## Building from source

Needs the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) or newer on Windows.

One project, two programs - `AppFlavor` in `BAModManager.csproj` picks which one is built:

```
build.bat                            → release\BAModManager-vX.Y.Z.zip       (Mod Manager)
build_launcher.bat                   → release\BAModLauncher-vX.Y.Z.zip      (Mod Launcher, modpack set up by the modder)
build_launcher.bat owner/repo        → release\<repo>-Launcher-vX.Y.Z.zip    (Mod Launcher with that modpack built in)
build_launcher.bat owner/repo owner/repo-testing  → the same, with a Stable / Testing switch
build_actualarrow.bat                → release\ActualArrow-Launcher-vX.Y.Z.zip
build_all.bat                        → all of the above
```

Each is one self-contained `.exe`. Versions come from `<Version>` in `BAModManager.csproj` (one for each program). In Visual Studio, the normal build is the Mod Manager; add `-p:AppFlavor=Launcher` (or set it in the project) for the launcher.

### Translations

All texts are in [`Core/Translations.cs`](Core/Translations.cs): the English text is the key, followed by German, French, Russian and Chinese. Anything missing is shown in English. Keep `{0}`, `{1}`… in every translation. Corrections from native speakers are welcome.

## Credits

- Made by **Speer**.
- Built on [MelonLoader](https://github.com/LavaGang/MelonLoader) by LavaGang - the Mod Manager downloads it from their GitHub releases.
- Font: [Inter](https://rsms.me/inter/), © The Inter Project Authors, [SIL Open Font License 1.1](Fonts/Inter-LICENSE.txt). The license file ships next to the exe.

This is a fan-made tool and isn't affiliated with or endorsed by the developers or publishers of Broken Arrow.

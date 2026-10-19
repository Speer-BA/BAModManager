<div align="center">

<img src="Assets/logo.png" width="96" alt="Broken Arrow Mod Manager logo">

# Broken Arrow Mod Manager

**Install MelonLoader and mods for Broken Arrow in a couple of clicks - and remove them just as easily.**

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

1. Download `BAModManager-vX.Y.Z.zip` from [Releases](../../releases/latest) and unzip it anywhere.
2. Run `BAModManager.exe`. It's a single file - no installer, no .NET to install.
3. The game folder is found automatically. If not, set it on the **Settings** page.
4. Go to **MelonLoader → Install MelonLoader**. The first game launch afterwards takes a few minutes - that's normal.
5. Drag mods onto the window (or double-click a `.bamod` file) and press **PLAY**.

> [!NOTE]
> The exe isn't code-signed, so Windows SmartScreen may show *"Windows protected your PC"*. Click **More info → Run anyway**.

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

**Version rules:** `"*"` any · `"1.2"` 1.2 or newer · `">=1.2, <2.0"` · `"=1.2.3"` exactly · `"^1.2"` any 1.x from 1.2 · `"~1.2"` any 1.2.x.

A requirement on a mod that players installed by hand (no `mod.json`) still works if its DLL is named like the last part of the id - `author.modelloader` matches `ModelLoader.dll`.

For plain MelonLoader mods, the Mod Manager also reads the name, version, author and description from your DLL's `[MelonInfo]` and assembly attributes.

**Plain zips and loose files** still work: a `.zip` laid out as above (without `mod.json`), or single `.dll` / `.bundle` files. One wrapper folder inside a zip (`MyMod-1.2/Mods/...`) is fine. Files outside `Mods/`, `UserData/`, `Plugins/` and `UserLibs/` are placed by the rules in **Settings → Where files go**.

## Where things are stored

| What | Where |
|---|---|
| Settings (language, game folder, file rules) | `%APPDATA%\BAModManager\settings.json` |
| What was installed, mod pictures and read-mes | `<game folder>\UserData\BAModManager\` |

**Settings → Uninstall Mod Manager…** removes the program, both of these folders and the `.bamod` file association. You choose whether MelonLoader and your mods stay (default) or go too.

## Building from source

Needs the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) or newer on Windows.

```
build.bat
```

This makes `publish\BAModManager.exe` (one self-contained file) and `release\BAModManager-vX.Y.Z.zip` ready to upload to GitHub Releases. The version comes from `<Version>` in `BAModManager.csproj`.

### Translations

All texts are in [`Core/Translations.cs`](Core/Translations.cs): the English text is the key, followed by German, French, Russian and Chinese. Anything missing is shown in English. Keep `{0}`, `{1}`… in every translation. Corrections from native speakers are welcome.

## Credits

- Made by **Speer**.
- Built on [MelonLoader](https://github.com/LavaGang/MelonLoader) by LavaGang - the Mod Manager downloads it from their GitHub releases.
- Font: [Inter](https://rsms.me/inter/), © The Inter Project Authors, [SIL Open Font License 1.1](Fonts/Inter-LICENSE.txt). The license file ships next to the exe.

This is a fan-made tool and isn't affiliated with or endorsed by the developers or publishers of Broken Arrow.

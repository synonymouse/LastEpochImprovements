## Better Item Filter and Tooltips

Lootfilter items visible on map. Rules custom drop sounds. Tooltip improvements and more

#### Mod on [NEXUSMODS]

---

### 📌 Features

1. Any item that passes filter check with **Emphasized** turned on will automatically be shown on map.
2. Hover item icon on map while holding **Left Shift** to show full item tooltip from any distance.
3. Enable **Item Affix Show** option in settings to display item affix roll values.
4. Attach a custom `.mp3` sound to any filter rule for personalized audio cues.

---

### 📦 How to Install

1. Download [MelonLoader.Installer.exe] and install MelonLoader.
    - The current Unity 6000.4 compatibility changes were verified with **MelonLoader 0.7.3**.
    - 📖 Installation Guide: [melonwiki]
    - Run `MelonLoader.Installer.exe`
        - Click the **SELECT** button.
        - Select the game's EXE in your **Last Epoch installation folder**.
        - Choose version (or leave default for latest).
        - Click **INSTALL** or **RE-INSTALL**.

2. Start the game and wait until it generates required `.dll` files.
3. Close the game.
4. Place my mod file inside the `Mods` folder.
5. Start the game.

---

### ⚠️ Disclaimer

I use this mod in Online mode without issues.  
However, modifying the game client is **against the Last Epoch Terms of Service**, so using it outside Offline mode is at your own risk.

---

### 🛠️ Troubleshooting

For Unity 6000.4 compatibility and the local MelonLoader/UnityExplorer fixes,
see [diagnostics](docs/diagnostics-2026-10-03.md) and the tool READMEs:

- [Automatic interop repair](tools/AutoRepairUnityInterop/README.md)
- [UnityExplorer bundle compatibility](tools/UnityExplorerCompat/README.md)
- [UnityExplorer scene API repair](tools/RepairExplorerScenes/README.md)

Build verification without deploying a DLL into the game:

```bash
dotnet build kg_LastEpoch_Improvements.sln -c Special -p:DeployToGame=false
```

**Issue:** Can't load mod due to ".NET 6.0" error  
**Fix:** [Download and install .NET 6.0 from Microsoft](https://dotnet.microsoft.com/en-us/download/dotnet/6.0)

**Issue:** Permission errors for files like `UnityEngineUI.dll`  
**Fix:** Add **MelonLoader** to your antivirus/firewall exceptions list to allow it to download required files.

---

### ❤️ Like my mods? Support me!

**PayPal:** war3spells@gmail.com

---

### 🔗 Links

* [NEXUSMODS]
* [melonwiki]
* [MelonLoader.Installer.exe]

---

[NEXUSMODS]: https://www.nexusmods.com/lastepoch/mods/8
[melonwiki]: https://melonwiki.xyz/#/
[MelonLoader.Installer.exe]: https://github.com/HerpDerpinstine/MelonLoader/releases/latest/download/MelonLoader.Installer.exe

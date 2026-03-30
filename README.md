# FogClimb

`FogClimb` turns PEAK's rising fog into a standalone climb mode for the host.

Current version: `0.0.6`

## Install

1. Install BepInEx for PEAK.
2. Put `com.github.Thanks.FogClimb.dll` into `BepInEx/plugins`.
3. Start the lobby as the host.
4. Adjust the settings in ModConfig if needed.

## What it does

- Runs as a host-only fog climb mod. Guests do not need to install it.
- Syncs fog movement and compass rewards for the room.
- Lets you tune fog speed, fog delay, HUD position, HUD scale, and a compass hotkey.
- Shows a lobby prompt for spawning a normal compass above your head, then hides that prompt after the run begins.
- Uses Chinese when the game language is Chinese, and English for other languages.

## Package contents

- `BepInEx/plugins/com.github.Thanks.FogClimb.dll`
  The mod itself. This is the only file you need to install.
- `manifest.json`
  Basic package metadata for loaders and releases.
- `icon.png`
  Package artwork.
- `README.md` and `CHANGELOG.md`
  Short notes for installation and version history.

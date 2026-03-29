# FogClimb

`FogClimb` turns PEAK's fog event into a clean, host-driven climb mode that can run as its own standalone mod.

This package is built so the host can install it once and everyone in the room can play. Guests do not need the mod installed to see the moving fog, follow the campfire beacon, or receive synced compass rewards.

Current version: `0.0.1`

## What this package includes

- `BepInEx/plugins/com.github.Thanks.FogClimb.dll`
  The actual mod file. This is the only file the host needs to place in the game folder.
- `manifest.json`
  Basic package metadata, useful if you want to manage releases or plug the package into a mod distribution workflow later.
- `README.md`
  A quick overview of what the mod does, how it behaves online, and where to install it.
- `CHANGELOG.md`
  A short release history for the current build.
- `icon.png`
  The package icon used for release distribution.

## What the mod does

- Pulls the fog-climb rules out of `ShootZombies` and ships them as a standalone mod.
- Lets you toggle the mode on or off.
- Lets you tune fog speed and the opening delay before the first movement starts.
- Shows a configurable HUD in both the lobby and in-game, with the title always shown as `FogClimb`.
- Gives every player a compass when the opening fog delay ends.
- Gives every player another compass whenever a campfire is lit.
- Spawns a bright host-synced beacon above the active campfire so players can spot the route more easily from a distance.
- Automatically uses Chinese config text when the game is running in Chinese, and English for other languages.

## Multiplayer behavior

- Host-only installation: only the room host needs this mod.
- Fog movement is driven by the host and synced to guests.
- Ambient fog cold damage is suppressed consistently for the whole lobby flow.
- Compass rewards are tracked by the host and re-synced, so late-joining players are less likely to miss them.

## Config options

- `Enable Mod`
- `Fog Speed`
- `Fog Delay (s)`
- `Fog UI`
- `UI X Position`
- `UI Y Position`
- `UI Scale`

## Installation

1. Install BepInEx for PEAK.
2. Put `com.github.Thanks.FogClimb.dll` into `BepInEx/plugins`.
3. Start the game as the host.
4. Adjust the mod settings in your config manager or config file if needed.

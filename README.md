# Fog&ColdControl

Fog&ColdControl is a host-driven PEAK mod that turns the climb into a rising-fog run with separate fog-cold and night-cold control.
Only the host needs to install it. Guests can join normally and follow the host's fog state without installing the mod.

Version: `1.0.1`

## Package contents

- `BepInEx/plugins/Thanks.Fog&ColdControl.dll`
  The mod itself.
- `README.md`
  A quick overview of features and settings.
- `CHANGELOG.md`
  Feature and fix history for the current release line.
- `manifest.json`
  Package metadata.
- `icon.png`
  Package icon.

## Main features

- Host-only fog control for offline and multiplayer runs.
- Synced fog timing, fog state, and cold handling for guests.
- Separate handling for fog cold and night cold.
- Compact bottom HUD for fog status, delay, distance, ETA, and state.
- Optional top-screen campfire locator HUD while the player is inside fog.
- Optional compass rewards and manual compass spawning.
- Automatic Chinese or English config names based on the current game language.
- Config file names, sections, keys, and descriptions that follow the current game language.
- `Caldera` and `The Kiln` stay disabled in this mode.

## Config sections

### Basic

- `Enable Mod`
  Master switch for the whole mod.
- `Suppress Fog Cold`
  Blocks cold caused by fog only.
- `Night Cold`
  Controls night cold separately from fog cold.
- `Fog Speed`
  Controls how fast the fog moves. Range: `0.3` to `20`. Default: `0.4`.
- `Fog Delay (s)`
  Controls how long the first fog segment waits before moving. Range: `20` to `1000`. Default: `900`.
- `Compass Feature`
  Enables or disables compass rewards, the compass hotkey, and the lobby prompt. Default: `OFF`.
- `Compass Hotkey`
  Spawns a normal compass in front of the player.
- `Pause Fog Hotkey`
  Host-only hotkey for pausing or resuming fog movement.
- `Fog UI`
  Shows or hides the bottom fog HUD.
- `Campfire Locator HUD`
  Shows or hides the top-screen campfire direction HUD. Default: `ON`.

### Adjustments

- `UI X Position`
  Moves the bottom fog HUD horizontally.
- `UI Y Position`
  Moves the bottom fog HUD vertically.
- `UI Scale`
  Changes the overall size of the bottom fog HUD.

## Compass behavior

- The opening fog start can grant a compass when the compass feature is enabled.
- Campfires only grant a compass when they actually begin a new fog segment.
- Final no-fog campfires do not grant compasses.
- The manual compass hotkey uses a normal compass item.
- The lobby prompt only appears when the compass feature is enabled and the hotkey is valid.

## Multiplayer behavior

- The host is the only side that controls fog state, fog timing, and cold correction.
- Guests without the mod can still join and play normally.
- Guests with the mod installed stay passive unless they become the host.

## Install

1. Install BepInEx for PEAK.
2. Put `Thanks.Fog&ColdControl.dll` into `BepInEx/plugins`.
3. Start the game and host a room, or play offline.

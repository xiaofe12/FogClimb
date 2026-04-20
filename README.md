# FogClimb

`FogClimb` turns PEAK's poison fog into a host-controlled climb mode for the whole room.

Current version: `1.1.1`

## At a glance

- Only the host needs to install FogClimb.
- Guests can join without installing anything.
- Offline and online use the same gameplay rules.
- The host controls fog movement, delay, compass rewards, HUD state, and cold handling.
- If a guest also has FogClimb installed, it stays passive unless that guest becomes the host.
- ModConfig text switches between Chinese and English with the game's current language.

## What the mod does

- Starts FogClimb by default.
- Lets the host control fog speed and fog delay from ModConfig.
- Shows a fog HUD in the lobby and during active gameplay.
- Gives players a normal compass after the opening fog delay and after each campfire lighting.
- Keeps Caldera and The Kiln fully fog-free in FogClimb.
- Preserves the intended late-game handling around Peak.

## Night cold handling

- `Suppress Fog Cold` only affects the cold caused by the fog itself.
- `Night Cold` only affects FogClimb's night-cold handling.
- Normal night cold is not removed by FogClimb.
- If `Night Cold` is enabled and vanilla night cold stops applying correctly, FogClimb can restore it from the host side.
- The game's official `Night Cold` setting still applies. If the official setting is off, FogClimb will not force night cold back on.
- Caldera and The Kiln keep the game's own late-game no-fog-cold behavior.

## Config options

- `Enabled`: turns FogClimb on or off.
- `Suppress Fog Cold`: removes fog-caused cold without removing normal night cold.
- `Night Cold`: controls FogClimb's night-cold handling only.
- `Fog Speed`: controls how fast the fog moves. Range: `0.3` to `20`.
- `Fog Delay`: controls how long the fog waits before moving after the buffer finishes. Range: `0` to `1000` seconds.
- `Compass Feature`: master switch for compass rewards, manual compass spawning, and the lobby prompt. Disabled by default.
- `Compass Hotkey`: spawns a normal compass in front of the local player.
- `Pause Fog Hotkey`: host-only hotkey to pause or resume fog movement.
- `Fog UI`: shows or hides the fog HUD.
- `Fog UI X Position`: moves the HUD horizontally.
- `Fog UI Y Position`: moves the HUD vertically.
- `Fog UI Scale`: changes the HUD size.

## Offline and online

- Offline: FogClimb runs fully local and uses the same rules as multiplayer.
- Online: only the host runs the main fog logic and syncs the result to the room.
- Guests do not need the mod for fog timing, compass rewards, or night-cold recovery to work.

## Install

1. Install BepInEx for PEAK.
2. Put `com.github.Thanks.FogClimb.dll` into `BepInEx/plugins`.
3. Launch the game and host a room, or play offline.

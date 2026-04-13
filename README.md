# FogClimb

`FogClimb` turns PEAK's poison fog into a standalone climb mode that the host can run for the whole room.

Current version: `1.0.1`

## What players need to know

- Only the host needs to install this mod.
- Guests can join without installing anything.
- The host drives fog movement, fog delay, compass rewards, and the related room sync.
- If a guest also has FogClimb installed, it now stays passive unless that guest becomes the host.
- The mod switches between Chinese and English automatically based on the game's current language.

## Main features

- FogClimb is enabled by default.
- ModConfig lets the host adjust fog speed, fog delay, fog-only cold handling, HUD visibility, HUD position, HUD scale, and the manual compass hotkey.
- The lobby shows a compass prompt, then hides it after the run begins.
- Normal night cold stays untouched.
- Kiln and Peak still keep the vanilla late-game behavior where players should not keep taking fog cold just because FogClimb is active.
- `Caldera Fog Position` and `Kiln Fog Position` are optional and stay off by default.

## Install

1. Install BepInEx for PEAK.
2. Put `com.github.Thanks.FogClimb.dll` into `BepInEx/plugins`.
3. Host a room and play.

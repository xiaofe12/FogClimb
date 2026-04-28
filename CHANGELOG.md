# Changelog

## 1.0.1

### Changes

- Let the game handle fog behavior from `Caldera` onward instead of forcing mod-managed fog coverage or synthetic fog stages there.

## 1.0.0

### Features

- Renamed the mod to `Fog&ColdControl` and aligned the DLL, config file, package name, and release metadata with the new name.
- Added host-driven rising fog control with synced fog state and timing for offline and multiplayer runs.
- Added separate controls for fog cold and night cold.
- Added optional compass rewards, a manual compass hotkey, and a lobby compass prompt.
- Added a compact fog HUD and an optional top-screen campfire locator HUD.
- Split config options into `Basic` and `Adjustments` sections.
- Added automatic Chinese and English config localization for ModConfig and the physical config file.
- Kept `Caldera` and `The Kiln` fully disabled in this mode.

### Fixes

- Fixed guest fog sync and host authority handling so guests can join without installing the mod.
- Fixed fog-cold handling so fog cold and night cold are corrected independently.
- Fixed language refresh so config names, descriptions, and file contents follow the current game language.
- Fixed compass reward timing so only real fog-start segments grant campfire compasses.

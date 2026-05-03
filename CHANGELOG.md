# Changelog

## 1.0.3

### Fixes

- Fixed fog-cold suppression clearing the entire cold status while night cold was enabled, which prevented vanilla night cold from affecting players.

## 1.0.2

### Changes

- Renamed the Chinese config panel label from `毒雾寒冷` to `毒雾寒冷阻止` to make the option's behavior clearer.
- Stabilized the fog ETA display so small sampling changes no longer make the countdown jump back and forth.
- Added the configured fog delay time, with a delay icon, to the lobby bottom HUD.
- Added a text day/night time entry to the in-game bottom HUD, with separate day and night icons.
- Moved `Enable Mod`, `Fog UI`, `Campfire Locator HUD`, `Compass Feature`, and `Compass Hotkey` into the `Adjustments` config section while migrating old `Basic` values.
- Kept guest fog distance and ETA visible after death by calculating them from the spectated character when available.
- Fixed fog-sourced cold being incorrectly suppressed when night cold was disabled but fog cold suppression was off.
- Fixed disabled night cold cleanup so it no longer uses the vanilla status-chunk removal path.
- Fixed host-managed multiplayer cold and spore corrections so installed guests and host-side character mirrors no longer use the vanilla status-chunk removal path.
- Fixed the host-managed status RPC patch parameter name so the plugin can finish loading and the HUD can initialize.
- Made cold handling fully host-authoritative: guests no longer use local cold settings, and installed guests receive the host's fog-cold and night-cold corrections.

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

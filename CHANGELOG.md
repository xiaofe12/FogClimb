# Changelog

## 1.1.1

- Reviewed the latest host log and found no new FogClimb runtime errors after the recent fog-start compatibility fix.
- Kept the hidden night test on `[` instead of `F8` to avoid accidental conflicts with other mods during testing.
- Kept the updated UI creation path that avoids the old RectTransform parent warning path seen in earlier runs.
- Bumped the release metadata to `1.1.1`.

## 1.1.0

- Added a separate `Night Cold` config entry so night-cold handling can be enabled or disabled independently from fog-cold suppression.
- Kept `Suppress Fog Cold` focused on fog-caused cold only, without changing the separate night-cold setting.
- Increased the `Fog Delay` config range to `0` through `1000` seconds.
- Added a `Compass Feature` master toggle for automatic compass rewards, the manual compass hotkey, and the lobby prompt.
- Kept the lobby compass prompt tied to the compass master toggle so it hides cleanly when compass features are disabled.
- Repacked the release as `1.1.0`.

## 1.0.3

- Caldera and The Kiln now stay fully disabled in FogClimb: the mod no longer advances fog origin into those stages.
- Disabled-stage behavior is now aligned between offline play and host-driven multiplayer sync.
- Rewrote the package text so the README explains the mod's features, night-cold handling, and config options more clearly.
- Kept the fog HUD countdown danger gradient and the host-side night-cold fallback from the `1.0.3` release line.
- Night-cold fallback still follows the official `Night Cold` setting automatically and still works for guests who do not install FogClimb.

## 1.0.2

- Fixed the ModConfig localization path so language switching no longer creates duplicate FogClimb section buttons.
- Switched FogClimb to stable internal config keys while keeping the visible UI localized to the current game language.
- Increased the maximum fog speed from `5` to `20`.
- Repacked the release as `1.0.2`.

## 1.0.1

- Added a hidden host-side night test shortcut: hold the internal test key for 5 seconds to force the current run into night for validation.
- Kept the night test hook out of ModConfig and player-facing UI.
- Tightened FogClimb's multiplayer behavior so guests stay passive and only the host drives the mode.
- Reduced unnecessary host-to-guest fog sync traffic by only pushing state updates when the fog snapshot actually changes.
- Kept host-side cold compensation focused on guests, so rooms no longer depend on guests having FogClimb installed.
- Repacked the release as `1.0.1`.

## 1.0.0

- Reworked fog-cold handling so FogClimb only suppresses the cold created by the fog itself and no longer removes normal night cold.
- Replaced the old blanket cold reset with source-based local blocking and safer host-side multiplayer compensation.
- Improved multiplayer compatibility by advertising FogClimb support between clients, so hosts avoid double-compensating guests who also have the mod installed.
- Refreshed the package text and release metadata for the `1.0.0` build.

## 0.0.6

- Added a configurable normal-compass hotkey. Default key: `G`.
- Hotkey-spawned compasses now always appear above the local player's head, avoiding inventory and hand-slot conflicts.
- Added an airport-lobby prompt based on ShootZombies to show which key spawns a compass, and it hides after entering the run.
- Updated localized config text and package metadata for the new hotkey feature.

## 0.0.5

- Removed the campfire beacon system from FogClimb.
- Fixed the fog HUD so it only appears in the airport lobby and active climb scenes, and keeps following the correct in-game HUD canvas.
- Unified version numbers across Plugin.cs, manifest.json, README.md, and the Thunderstore packaging script.
- Cleaned up legacy build artifacts from bin/Release directory.

## 0.0.4

- Added runtime localization refresh so FogClimb can update its ModConfig labels after the game language changes.
- Improved the fog HUD so it follows the active HUD canvas instead of only appearing in the lobby or right after entering a run.

## 0.0.3

- Kept the HUD styling aligned with the game's own font presentation.

## 0.0.2

- Switched compass rewards to the standard compass path and exposed a manual `itemID` override entry for debugging.
- When a player's inventory is full during wall climbing, the compass now spawns above the player's head instead of occupying the held-item slot.
- Added cleanup for generated `com.github.Thanks.FogClimb.dll-unrpcpatched.old` backup files at startup.

## 0.0.1

- Split the toxic fog mode out of ShootZombies and rebuilt it as a standalone FogClimb mod.
- Added host-side config for enable/disable, fog speed, fog delay, HUD toggle, HUD position, and HUD scale.
- Added automatic Chinese or English config labels based on the game's local language.
- Added host-synced campfire beacon visuals for easier route finding.
- Added synced compass rewards after the opening fog delay and after each campfire lighting.
- Improved multiplayer recovery by letting the host catch players up on missed compass rewards.

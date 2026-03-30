# Changelog

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
- Refreshed the package icon set with five new illustration-style variants focused on climbing, toxic fog, and compass guidance without the campfire beacon.

## 0.0.3

- Removed the extra `FogClimb` title text from the fog HUD so the panel stays cleaner in both the lobby and during a run.
- Kept the HUD styling aligned with the game's own font presentation.
- Refreshed the package artwork for the standalone FogClimb release.
- Updated package metadata and release output to version `0.0.3`.

## 0.0.2

- Switched compass rewards to the standard compass path and exposed a manual `itemID` override entry for debugging.
- When a player's inventory is full during wall climbing, the compass now spawns above the player's head instead of occupying the held-item slot.
- Added cleanup for generated `com.github.Thanks.FogClimb.dll-unrpcpatched.old` backup files at startup.

## 0.0.1

- Split the toxic fog mode out of `ShootZombies` and rebuilt it as a standalone `FogClimb` mod.
- Added host-side config for enable/disable, fog speed, fog delay, HUD toggle, HUD position, and HUD scale.
- Added automatic Chinese or English config labels based on the game's local language.
- Added host-synced campfire beacon visuals for easier route finding.
- Added synced compass rewards after the opening fog delay and after each campfire lighting.
- Improved multiplayer recovery by letting the host catch players up on missed compass rewards.

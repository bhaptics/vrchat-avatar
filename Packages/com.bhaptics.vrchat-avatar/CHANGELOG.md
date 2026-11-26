# Changelog

## [3.0.0] - Unreleased

A rewrite of the legacy bHaptics VRChat package (1.x–2.x). It keeps the OSC contract the bHaptics
Player reads, so avatars set up with either work with the Player; set existing avatars up again
to use this version.

### Added

- **bHaptics Setup** component: add, adjust, reset and remove devices from one inspector, with
  every change undoable.
- Devices placed from the avatar's rig: TactVisor, TactSuit Pro and Air, TactSleeve and TactGlove.
- Device models to place devices by eye, and indicators that light up where you're touched, for
  you and for others (PC).
- PC and Quest receivers on every device, and **Switch Receivers** to match the build target.
- **Apply Indicators**, which works on copies of the FX controller and Expression Parameters and
  never edits the originals.
- Upload checks that stop an avatar the Player would misread: PC and Quest receivers together,
  the other platform's parameters in the FX, or more than 16 contacts on Quest.
- **Remove bHaptics from Avatar**, listing what it deletes first.
- Warnings for what the legacy package left on the avatar and in the project.
- An Avatar Optimizer asset description that keeps the receivers, and `ParameterExclusions.txt`
  for DexProtect.

### Changed from the legacy package

- Receivers are Local Only: they no longer count toward the performance rank or Quest's contact
  limit, and others see the indicators through synced parameters.
- The current TactSuit and TactSleeve drive two contract nodes with one motor; such a pair is one
  node with both nodes' receivers, so older devices still get every address.

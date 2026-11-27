# bHaptics Avatar for VRChat

Source of `com.bhaptics.vrchat-avatar`, the VPM package that adds bHaptics haptic devices to
VRChat avatars. Version 3 is a rewrite of the legacy package,
[bhaptics/VRChatOSC](https://github.com/bhaptics/VRChatOSC), and keeps its OSC contract
(version 2), so the bHaptics Player reads avatars from both.

User documentation: [How to play VRChat with bHaptics](https://bhaptics.notion.site/How-to-play-VRChat-with-bHaptics-1226d5724b8b80229ab9e0001ab70b61)

## Layout

- `Packages/com.bhaptics.vrchat-avatar/` — the package, and the only thing that ships
- `Contract/` — the OSC contract reference: the receivers the Player expects
- `Assets/_/Tests/` — the editor tests, kept out of the package
- `.github/workflows/release.yml` — builds the release; the VPM listing is built in
  [bhaptics/vpm](https://github.com/bhaptics/vpm) from this repository's releases

[AGENTS.md](AGENTS.md) holds the conventions, the contract and the architecture, for people and
coding agents alike.

## Working on it

1. Open the project with Unity 2022.3.22f1 through VCC or ALCOM.
2. Add hai-vr's listing (`https://hai-vr.github.io/vpm-listing/index.json`) so Animator As Code
   resolves.
3. Run the tests from **Window > General > Test Runner**, EditMode, assembly
   `com.bhaptics.vrchat-avatar.Tests`.

Work on a branch and merge by pull request with a merge commit; split commits by intent.

## Releasing

The **Build Release** workflow packs the package at the version in its `package.json` into a zip
and a `.unitypackage`, and publishes them as a GitHub release, marked as a pre-release when the
version has a suffix such as `-preview.1`. It reads the package name from the `PACKAGE_NAME`
repository variable. The bHaptics VPM listing (`https://vpm.bhaptics.com/index.json`), built in
[bhaptics/vpm](https://github.com/bhaptics/vpm), picks the release up from there.

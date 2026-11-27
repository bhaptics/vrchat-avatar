# bHaptics Avatar

Adds bHaptics haptic devices to VRChat avatars. Contact receivers on the avatar report where it's
touched, and the bHaptics Player plays that on your devices.

Full guide: [How to play VRChat with bHaptics](https://bhaptics.notion.site/How-to-play-VRChat-with-bHaptics-1226d5724b8b80229ab9e0001ab70b61)

## Requirements

- Unity 2022.3 and VRChat SDK - Avatars 3.10.0 or later
- Animator As Code V1 - VRChat, from hai-vr's listing
  (`https://hai-vr.github.io/vpm-listing/index.json`), for indicators
- The bHaptics Player, running while you play

## Install

Add the bHaptics listing to VCC or ALCOM (`https://bhaptics.github.io/vpm/index.json`), then add
**bHaptics Avatar** to your avatar project.

## Quick start

1. Add **bHaptics Setup** to your avatar root, the object with the VRC Avatar Descriptor.
2. Click **Add Device** and pick your devices.
3. Open each device's row and adjust its **Offset** so the device sits where you wear it. **Show
   Mesh** shows the device model to help.
4. Click **Apply Indicators** so the spots you're touched on light up, for you and for others.
5. Upload as usual.

Every change can be undone with Ctrl+Z.

## PC and Quest

Each device carries receivers for both platforms, and the build target decides which set uploads.
After you switch the build target, click **Switch Receivers**; uploads are blocked until you do.
On Quest, VRChat allows at most 16 contacts that aren't Local Only.

Indicators don't show on Quest yet; we're looking into showing them there. The Quest build still
carries their synced parameters: VRChat matches synced parameters between the PC and Quest builds
by position, so leaving them out would shift the ones after them and put your other parameters
out of step. To free those bits on both builds, turn **Show Indicator** off for the device.

## Moving from the legacy package

Avatars set up with the legacy package (1.x–2.x) need to be set up again. The inspector lists
what the legacy package left behind; delete all of it, or bHaptics may malfunction. Delete the
receivers **Select Receivers** picks and the `Assets/bHapticsOSC` folder, then click **Apply
Indicators** to take the legacy layers out of the FX. If the legacy
guide had you import Animator As Code into `Assets`, delete that folder too unless another tool
uses it.

## What Apply Indicators changes

Apply never edits your FX controller or Expression Parameters. It copies them into
`Assets/bHaptics/Generated/`, changes the copies and points the avatar at them. Each indicator
takes one synced parameter bit; Apply refuses to take the avatar past VRChat's 256.

An avatar without an Expressions Menu of its own gets an empty one, since the VRChat SDK won't
upload Expression Parameters without a menu. It replaces VRChat's default emotes.

## Removing

Use **Remove bHaptics from Avatar…** at the bottom of the inspector or in the component's ⋮ menu.
It lists what it will delete before it does anything.

## Known limitations

- Indicators show on PC only, and gloves have none.
- Device models and indicators use VRChat's Standard Lite shader on PC too, since the SDK's Quest
  shader check also reads the hidden PC set. They take no shadows and only the main directional
  light lights them per pixel, so they look a little plainer than with Unity's Standard shader.
- An Animator Override Controller in the FX slot isn't supported: Apply asks for an Animator
  Controller there.
- On an avatar without a Humanoid rig, devices are placed at the avatar root.

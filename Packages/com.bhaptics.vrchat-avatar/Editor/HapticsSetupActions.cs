using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace bHaptics.VRChatAvatar
{
    /// Undoable device actions on a HapticsSetup. Each one refuses a prefab asset, returning null, false or 0; Add also refuses a setup off the avatar root.
    /// Undo.RecordObject(setup) goes right before the write: another Undo call in between leaves the write unrecorded.
    public static class HapticsSetupActions
    {
        /// Returns the new device, or null when setup isn't on the avatar root or already has one with this definition.
        public static HapticDevice AddDevice(HapticsSetup setup, DeviceDefinition definition)
        {
            if (!IsEditable(setup))
            {
                return null;
            }

            if (!IsOnAvatarRoot(setup))
            {
                return null;
            }

            if (setup.FindDevice(definition) != null)
            {
                Debug.LogWarning($"[bHaptics] {definition.displayName} is already on this avatar.", setup);
                return null;
            }

            var undoName = $"Add bHaptics {definition.displayName}";
            var device = DeviceBuilder.CreateDevice(setup.transform, definition);

            Undo.RegisterCreatedObjectUndo(device.deviceRoot.gameObject, undoName);
            Undo.RecordObject(setup, undoName);

            setup.devices.Add(device);

            PrefabUtility.RecordPrefabInstancePropertyModifications(setup);

            Debug.Log($"[bHaptics] Added {definition.displayName} under {device.deviceRoot.parent.name}.", device.deviceRoot);

            return device;
        }

        /// Remove the device for definition, with its objects. Returns false when setup doesn't have one.
        public static bool RemoveDevice(HapticsSetup setup, DeviceDefinition definition)
        {
            if (!IsEditable(setup))
            {
                return false;
            }

            var device = setup.FindDevice(definition);
            if (device == null)
            {
                Debug.LogWarning($"[bHaptics] {definition.displayName} isn't on this avatar.", setup);
                return false;
            }

            var undoName = $"Remove bHaptics {definition.displayName}";
            Undo.SetCurrentGroupName(undoName);

            // The user may have deleted the device root already; then only the entry goes.
            if (device.deviceRoot != null)
            {
                Undo.DestroyObjectImmediate(device.deviceRoot.gameObject);
            }

            Undo.RecordObject(setup, undoName);
            setup.devices.Remove(device);
            PrefabUtility.RecordPrefabInstancePropertyModifications(setup);

            Debug.Log($"[bHaptics] Removed {definition.displayName}.", setup);
            return true;
        }

        /// Rebuild the receiver sets and meshes of the device for definition under its Offset; other children of the Offset stay.
        /// Returns false when setup doesn't have the device or its Offset is gone.
        public static bool ResetDevice(HapticsSetup setup, DeviceDefinition definition)
        {
            if (!IsEditable(setup))
            {
                return false;
            }

            var device = setup.FindDevice(definition);
            if (device == null)
            {
                Debug.LogWarning($"[bHaptics] {definition.displayName} isn't on this avatar.", setup);
                return false;
            }

            if (device.offset == null)
            {
                Debug.LogWarning($"[bHaptics] {definition.displayName} has no Offset; remove it and add it again.", setup);
                return false;
            }

            var undoName = $"Reset bHaptics {definition.displayName}";
            Undo.SetCurrentGroupName(undoName);

            // Only our sets go, wherever the user moved them; anything else under the Offset stays.
            if (device.pc != null)
            {
                Undo.DestroyObjectImmediate(device.pc);
            }

            if (device.quest != null)
            {
                Undo.DestroyObjectImmediate(device.quest);
            }

            // CreateReceiverSets writes device.pc and device.quest, so record setup first.
            Undo.RecordObject(setup, undoName);
            DeviceBuilder.CreateReceiverSets(device);
            DeviceBuilder.AddMeshes(device);

            Undo.RegisterCreatedObjectUndo(device.pc, undoName);
            Undo.RegisterCreatedObjectUndo(device.quest, undoName);
            PrefabUtility.RecordPrefabInstancePropertyModifications(setup);

            Debug.Log($"[bHaptics] Reset {definition.displayName}.", device.deviceRoot);
            return true;
        }

        /// Make every device upload the build target's receiver set, as a newly added one would.
        /// Returns how many devices changed: 0 when none had to.
        public static int SwitchReceiverSets(HapticsSetup setup)
        {
            if (!IsEditable(setup))
            {
                return 0;
            }

            var isMobile = Platforms.IsBuildTargetMobile();
            var undoName = "Switch bHaptics Receivers";
            var switched = 0;
            foreach (var device in setup.devices)
            {
                if (Platforms.MatchesPlatform(device, isMobile))
                {
                    continue;
                }

                // One call for both sets: a second RecordObject between the first and its write could leave it unrecorded.
                var sets = GetExistingSets(device);
                Undo.RecordObjects(sets, undoName);
                Platforms.SelectUploadedSet(device, isMobile);
                foreach (var set in sets)
                {
                    PrefabUtility.RecordPrefabInstancePropertyModifications(set);
                }

                switched++;
            }

            Debug.Log($"[bHaptics] Switched {switched} devices to the {(isMobile ? "Quest" : "PC")} receivers.", setup);
            return switched;
        }

        /// Show or hide the meshes of the device for definition; hidden ones stay in the scene, inactive and EditorOnly, so they never upload.
        /// Returns false when setup doesn't have the device.
        public static bool SetMeshShown(HapticsSetup setup, DeviceDefinition definition, bool shown)
        {
            if (!IsEditable(setup))
            {
                return false;
            }

            var device = setup.FindDevice(definition);
            if (device == null)
            {
                Debug.LogWarning($"[bHaptics] {definition.displayName} isn't on this avatar.", setup);
                return false;
            }

            var undoName = $"{(shown ? "Show" : "Hide")} bHaptics {definition.displayName} Mesh";
            // One call for the setup and the meshes: a second RecordObject between the first and its write could leave it unrecorded.
            var targets = new List<Object> { setup };
            if (device.pcMesh != null)
            {
                targets.Add(device.pcMesh);
            }

            if (device.questMesh != null)
            {
                targets.Add(device.questMesh);
            }

            Undo.RecordObjects(targets.ToArray(), undoName);
            device.showMesh = shown;
            DeviceBuilder.ApplyShowMesh(device);
            foreach (var target in targets)
            {
                PrefabUtility.RecordPrefabInstancePropertyModifications(target);
            }

            Debug.Log($"[bHaptics] {(shown ? "Showed" : "Hid")} the {definition.displayName} mesh.", setup);
            return true;
        }

        /// Get device's receiver sets that still exist; the user may have deleted one.
        private static GameObject[] GetExistingSets(HapticDevice device)
        {
            var sets = new List<GameObject>();
            if (device.pc != null)
            {
                sets.Add(device.pc);
            }

            if (device.quest != null)
            {
                sets.Add(device.quest);
            }

            return sets.ToArray();
        }

        /// Returns false, with a warning, when setup is a prefab asset, whose children can't be added or removed here.
        private static bool IsEditable(HapticsSetup setup)
        {
            if (EditorUtility.IsPersistent(setup))
            {
                Debug.LogWarning("[bHaptics] Open the prefab, or select an instance in a scene, to change its devices.", setup);
                return false;
            }

            return true;
        }

        /// Returns false, with a warning, when setup isn't next to a VRC Avatar Descriptor; device placement measures from the avatar root.
        private static bool IsOnAvatarRoot(HapticsSetup setup)
        {
            if (!Placement.IsAvatarRoot(setup.transform))
            {
                Debug.LogWarning("[bHaptics] Put bHaptics Setup on the avatar root, next to its VRC Avatar Descriptor.", setup);
                return false;
            }

            return true;
        }
    }
}

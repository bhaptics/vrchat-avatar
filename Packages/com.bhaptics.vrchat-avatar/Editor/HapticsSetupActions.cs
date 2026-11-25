using System.Collections.Generic;
using System.Linq;
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
            RegisterCreatedParts(device, undoName);
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

            // An entry pasted from another avatar points at that avatar's objects; then only the entry goes.
            if (setup.IsOnThisAvatar(device))
            {
                // The user may have deleted the device root already.
                if (device.deviceRoot != null)
                {
                    Undo.DestroyObjectImmediate(device.deviceRoot.gameObject);
                }

                DestroyParts(device);
            }

            Undo.RecordObject(setup, undoName);
            setup.devices.Remove(device);
            PrefabUtility.RecordPrefabInstancePropertyModifications(setup);

            Debug.Log($"[bHaptics] Removed {definition.displayName}.", setup);
            return true;
        }

        /// Rebuild the receiver sets, meshes and indicator of the device for definition under its Offset; other children of the Offset stay.
        /// Returns false when setup doesn't have the device or its Offset is gone.
        public static bool ResetDevice(HapticsSetup setup, DeviceDefinition definition)
        {
            var device = FindEditableDevice(setup, definition);
            if (device == null)
            {
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

            DestroyParts(device);

            // CreateReceiverSets writes device.pc, device.quest and the parts, so record setup first.
            Undo.RecordObject(setup, undoName);
            DeviceBuilder.CreateReceiverSets(setup.transform, device);
            DeviceBuilder.AddMeshes(device);
            DeviceBuilder.AddIndicator(device);

            Undo.RegisterCreatedObjectUndo(device.pc, undoName);
            Undo.RegisterCreatedObjectUndo(device.quest, undoName);
            RegisterCreatedParts(device, undoName);
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
                // Another avatar's objects are that avatar's to switch.
                if (Platforms.MatchesPlatform(device, isMobile) || !setup.IsOnThisAvatar(device))
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
            var device = FindEditableDevice(setup, definition);
            if (device == null)
            {
                return false;
            }

            var undoName = $"{(shown ? "Show" : "Hide")} bHaptics {definition.displayName} Mesh";
            // One call for the setup and the meshes: a second RecordObject between the first and its write could leave it unrecorded.
            var targets = new List<Object> { setup };
            if (device.pcMeshObject != null)
            {
                targets.Add(device.pcMeshObject);
            }

            if (device.questMeshObject != null)
            {
                targets.Add(device.questMeshObject);
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

        /// Show or hide the indicator of the device for definition; a hidden one stays in the scene, inactive and EditorOnly, so it never uploads.
        /// Returns false when setup doesn't have the device.
        public static bool SetIndicatorShown(HapticsSetup setup, DeviceDefinition definition, bool shown)
        {
            var device = FindEditableDevice(setup, definition);
            if (device == null)
            {
                return false;
            }

            var undoName = $"{(shown ? "Show" : "Hide")} bHaptics {definition.displayName} Indicator";
            // One call for the setup and the indicator: a second RecordObject between the first and its write could leave it unrecorded.
            var targets = new List<Object> { setup };
            if (device.pcIndicatorObject != null)
            {
                targets.Add(device.pcIndicatorObject);
            }

            Undo.RecordObjects(targets.ToArray(), undoName);
            device.showIndicator = shown;
            DeviceBuilder.ApplyShowIndicator(device);
            foreach (var target in targets)
            {
                PrefabUtility.RecordPrefabInstancePropertyModifications(target);
            }

            Debug.Log($"[bHaptics] {(shown ? "Showed" : "Hid")} the {definition.displayName} indicator.", setup);
            return true;
        }

        /// Take bHaptics off setup's avatar: the FX and Expression Parameters slots get copies without our layers, parameters and Bools,
        /// then the device objects go, then the component. One Ctrl+Z brings it all back; the copies stay as files. Returns false for a prefab asset.
        public static bool RemoveSetup(HapticsSetup setup)
        {
            if (!IsEditable(setup))
            {
                return false;
            }

            var undoName = "Remove bHaptics from Avatar";
            var avatar = setup.gameObject;
            var plan = IndicatorPlanner.PlanRemoval(setup);
            // Without a descriptor there are no slots to clean, only objects.
            var copies = plan.blockers.Count == 0 && !plan.IsUpToDate;
            if (copies)
            {
                IndicatorApplier.Apply(setup, plan);
            }

            // An entry pasted from another avatar points at that avatar's objects; those stay.
            foreach (var device in setup.devices.Where(setup.IsOnThisAvatar))
            {
                if (device.deviceRoot != null)
                {
                    Undo.DestroyObjectImmediate(device.deviceRoot.gameObject);
                }

                DestroyParts(device);
            }

            Undo.DestroyObjectImmediate(setup);
            Undo.SetCurrentGroupName(undoName);
            Debug.Log($"[bHaptics] Removed bHaptics from {avatar.name}. Ctrl+Z brings it back{(copies ? $"; the copies made stay in {GeneratedAssets.Folder}" : "")}.", avatar);
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

            sets.AddRange(device.pcParts.FindAll(part => part != null));
            sets.AddRange(device.questParts.FindAll(part => part != null));
            return sets.ToArray();
        }

        /// Destroy device's parts that still exist, recorded for Undo; they sit under finger bones, outside the device root.
        private static void DestroyParts(HapticDevice device)
        {
            foreach (var part in device.pcParts.Concat(device.questParts).ToList())
            {
                if (part != null)
                {
                    Undo.DestroyObjectImmediate(part);
                }
            }
        }

        /// Record device's parts as created, under undoName.
        private static void RegisterCreatedParts(HapticDevice device, string undoName)
        {
            foreach (var part in device.pcParts.Concat(device.questParts))
            {
                Undo.RegisterCreatedObjectUndo(part, undoName);
            }
        }

        /// Returns definition's device on setup, or null, with a warning, when setup is a prefab asset, has no such device, or the device's objects are on another avatar.
        private static HapticDevice FindEditableDevice(HapticsSetup setup, DeviceDefinition definition)
        {
            if (!IsEditable(setup))
            {
                return null;
            }

            var device = setup.FindDevice(definition);
            if (device == null)
            {
                Debug.LogWarning($"[bHaptics] {definition.displayName} isn't on this avatar.", setup);
                return null;
            }

            if (!setup.IsOnThisAvatar(device))
            {
                Debug.LogWarning($"[bHaptics] {definition.displayName}'s objects are on another avatar; remove it here and add it again.", setup);
                return null;
            }

            return device;
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

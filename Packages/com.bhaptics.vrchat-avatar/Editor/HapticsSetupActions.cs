using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace bHaptics.VRChatAvatar
{
    /// Undoable device actions on a HapticsSetup. Each one refuses a prefab asset, returning null or false; Add also refuses a setup off the avatar root.
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
            if (setup.GetComponent<VRCAvatarDescriptor>() == null)
            {
                Debug.LogWarning("[bHaptics] Put bHaptics Setup on the avatar root, next to its VRC Avatar Descriptor.", setup);
                return false;
            }

            return true;
        }
    }
}

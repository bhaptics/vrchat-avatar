using UnityEditor;
using UnityEngine;

namespace bHaptics.VRChatAvatar
{
    public static class HapticsSetupActions
    {
        /// Returns the new device, or null when setup already has one with this definition.
        public static HapticDevice AddDevice(HapticsSetup setup, DeviceDefinition definition)
        {
            if (setup.devices.Exists(d => d.definition == definition))
            {
                Debug.LogWarning($"[bHaptics] {definition.displayName} is already on this avatar.", setup);
                return null;
            }

            var undoName = $"Add bHaptics {definition.displayName}";
            var device = DeviceBuilder.CreateDevice(setup, definition);

            Undo.RegisterCreatedObjectUndo(device.deviceRoot.gameObject, undoName);
            Undo.RecordObject(setup, undoName);

            setup.devices.Add(device);

            PrefabUtility.RecordPrefabInstancePropertyModifications(setup);

            Debug.Log($"[bHaptics] Added {definition.displayName} under {device.deviceRoot.parent.name}.", device.deviceRoot);

            return device;
        }

        /// Remove the device's objects and its entry. Returns false when setup doesn't hold device.
        public static bool RemoveDevice(HapticsSetup setup, HapticDevice device)
        {
            var index = setup.devices.IndexOf(device);
            if (index < 0)
            {
                Debug.LogWarning("[bHaptics] This device isn't on this avatar.", setup);
                return false;
            }

            var undoName = $"Remove bHaptics {device.definition.displayName}";
            Undo.SetCurrentGroupName(undoName);

            // The user may have deleted the device root already; then only the entry goes.
            if (device.deviceRoot != null)
            {
                Undo.DestroyObjectImmediate(device.deviceRoot.gameObject);
            }

            Undo.RecordObject(setup, undoName);
            setup.devices.RemoveAt(index);
            PrefabUtility.RecordPrefabInstancePropertyModifications(setup);

            Debug.Log($"[bHaptics] Removed {device.definition.displayName}.", setup);
            return true;
        }

        /// Rebuild everything under the device's Offset from its definition, keeping the Offset itself.
        /// Returns false when setup doesn't hold device or its Offset is gone.
        public static bool ResetDevice(HapticsSetup setup, HapticDevice device)
        {
            if (!setup.devices.Contains(device))
            {
                Debug.LogWarning("[bHaptics] This device isn't on this avatar.", setup);
                return false;
            }

            if (device.offset == null)
            {
                Debug.LogWarning($"[bHaptics] {device.definition.displayName} has no Offset; recreate it instead.", setup);
                return false;
            }

            var undoName = $"Reset bHaptics {device.definition.displayName}";
            Undo.SetCurrentGroupName(undoName);

            for (var i = device.offset.childCount - 1; i >= 0; i--)
            {
                Undo.DestroyObjectImmediate(device.offset.GetChild(i).gameObject);
            }

            // CreateReceiverSets writes device.pc and device.quest, so record setup first.
            Undo.RecordObject(setup, undoName);
            DeviceBuilder.CreateReceiverSets(device);

            Undo.RegisterCreatedObjectUndo(device.pc, undoName);
            Undo.RegisterCreatedObjectUndo(device.quest, undoName);
            PrefabUtility.RecordPrefabInstancePropertyModifications(setup);

            Debug.Log($"[bHaptics] Reset {device.definition.displayName}.", device.deviceRoot);
            return true;
        }

        /// Create the device's objects again for an entry whose device root is gone.
        /// Returns the new entry, or null when setup doesn't hold device or its device root still exists.
        public static HapticDevice RecreateDevice(HapticsSetup setup, HapticDevice device)
        {
            var index = setup.devices.IndexOf(device);
            if (index < 0)
            {
                Debug.LogWarning("[bHaptics] This device isn't on this avatar.", setup);
                return null;
            }

            if (device.deviceRoot != null)
            {
                Debug.LogWarning($"[bHaptics] {device.definition.displayName} still has its device root; reset it instead.", setup);
                return null;
            }

            var undoName = $"Recreate bHaptics {device.definition.displayName}";
            var rebuilt = DeviceBuilder.CreateDevice(setup, device.definition);

            Undo.RegisterCreatedObjectUndo(rebuilt.deviceRoot.gameObject, undoName);
            Undo.RecordObject(setup, undoName);

            setup.devices[index] = rebuilt;

            PrefabUtility.RecordPrefabInstancePropertyModifications(setup);

            Debug.Log($"[bHaptics] Recreated {device.definition.displayName}.", rebuilt.deviceRoot);
            return rebuilt;
        }
    }
}

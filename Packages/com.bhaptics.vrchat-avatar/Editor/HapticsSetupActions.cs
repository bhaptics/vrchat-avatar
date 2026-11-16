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
            var avatarRoot = setup.transform;
            var parent = Placement.GetDeviceRootParent(avatarRoot, definition.bone);

            var deviceRoot = CreateChild($"bHaptics {definition.displayName}", parent);
            deviceRoot.localRotation = Placement.GetDeviceRootRotation(avatarRoot, parent);
            deviceRoot.localScale = Placement.GetDeviceRootScale(parent);

            var offset = CreateChild("Offset", deviceRoot);
            Undo.RegisterCreatedObjectUndo(deviceRoot.gameObject, undoName);

            var device = new HapticDevice { definition = definition, deviceRoot = deviceRoot, offset = offset };
            Undo.RecordObject(setup, undoName);

            setup.devices.Add(device);

            PrefabUtility.RecordPrefabInstancePropertyModifications(setup);

            Debug.Log($"[bHaptics] Added {definition.displayName} under {parent.name}.", deviceRoot);
            return device;
        }

        /// Returns a new empty child of parent with an identity local transform.
        private static Transform CreateChild(string name, Transform parent)
        {
            var child = new GameObject(name).transform;
            child.SetParent(parent, false);
            return child;
        }
    }
}

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using VRC.Dynamics;
using VRC.SDK3.Dynamics.Contact.Components;

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

            var avatarRoot = setup.transform;
            var parent = Placement.GetDeviceRootParent(avatarRoot, definition.bone);
            var undoName = $"Add bHaptics {definition.displayName}";

            // STEP 1. Device Root
            var deviceRoot = CreateChild($"bHaptics {definition.displayName}", parent);
            deviceRoot.localRotation = Placement.GetDeviceRootRotation(avatarRoot, parent);
            deviceRoot.localScale = Placement.GetDeviceRootScale(parent);

            // STEP 2. Offset
            var offset = CreateChild("Offset", deviceRoot);

            // STEP 3. Receivers
            var device = new HapticDevice
            {
                definition = definition,
                deviceRoot = deviceRoot,
                offset = offset,
                pc = CreateReceiverSet("PC", offset, definition.pc),
                quest = CreateReceiverSet("Quest", offset, definition.quest),
            };

            SetActiveReceiverSet(device, IsBuildTargetMobile());

            Undo.RegisterCreatedObjectUndo(deviceRoot.gameObject, undoName);
            Undo.RecordObject(setup, undoName);

            setup.devices.Add(device);

            PrefabUtility.RecordPrefabInstancePropertyModifications(setup);

            Debug.Log($"[bHaptics] Added {definition.displayName} under {parent.name}.", deviceRoot);

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

            var pc = CreateReceiverSet("PC", device.offset, device.definition.pc);
            var quest = CreateReceiverSet("Quest", device.offset, device.definition.quest);

            Undo.RecordObject(setup, undoName);
            device.pc = pc;
            device.quest = quest;
            SetActiveReceiverSet(device, IsBuildTargetMobile());

            Undo.RegisterCreatedObjectUndo(pc, undoName);
            Undo.RegisterCreatedObjectUndo(quest, undoName);
            PrefabUtility.RecordPrefabInstancePropertyModifications(setup);

            Debug.Log($"[bHaptics] Reset {device.definition.displayName}.", device.deviceRoot);
            return true;
        }


        /// Returns a new empty child of parent with an identity local transform.
        private static Transform CreateChild(string name, Transform parent)
        {
            var child = new GameObject(name).transform;
            child.SetParent(parent, false);
            return child;
        }

        /// Create a child of offset with one node per definition node, each carrying its receivers. Returns the set.
        private static GameObject CreateReceiverSet(string name, Transform offset, DeviceDefinition.Node[] nodes)
        {
            var set = CreateChild(name, offset);
            foreach (var node in nodes)
            {
                var nodeTransform = CreateChild(node.name, set);
                nodeTransform.localPosition = node.position;
                nodeTransform.localEulerAngles = node.rotation;
                foreach (var receiver in node.receivers)
                {
                    AddReceiver(nodeTransform.gameObject, node, receiver);
                }
            }

            return set.gameObject;
        }

        /// Add receiver children to target.
        private static void AddReceiver(GameObject target, DeviceDefinition.Node node, DeviceDefinition.Receiver spec)
        {
            var receiver = target.AddComponent<VRCContactReceiver>();
            receiver.shapeType = node.shape;
            receiver.radius = node.radius;
            receiver.height = node.height;
            receiver.receiverType = ContactReceiver.ReceiverType.Proximity;
            receiver.localOnly = true;
            receiver.parameter = spec.address;
            receiver.allowSelf = spec.allowSelf;
            receiver.allowOthers = spec.allowOthers;
            receiver.collisionTags = new List<string>(spec.tags);
        }

        private static bool IsBuildTargetMobile()
        {
            var target = EditorUserBuildSettings.activeBuildTarget;
            return target is BuildTarget.Android or BuildTarget.iOS;
        }

        /// Turn on the set for the platform and turn off the other one, tagged EditorOnly so it never uploads.
        private static void SetActiveReceiverSet(HapticDevice device, bool isMobile)
        {
            SetUploaded(device.pc, !isMobile);
            SetUploaded(device.quest, isMobile);
        }

        /// Activate the set and clear its tag, or deactivate it and tag it EditorOnly.
        private static void SetUploaded(GameObject set, bool uploaded)
        {
            set.SetActive(uploaded);
            set.tag = uploaded ? "Untagged" : "EditorOnly";
        }
    }
}

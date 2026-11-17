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

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
            
            // UNDO START
            var undoName = $"Add bHaptics {definition.displayName}";

            var deviceRoot = CreateChild($"bHaptics {definition.displayName}", parent);
            deviceRoot.localRotation = Placement.GetDeviceRootRotation(avatarRoot, parent);
            deviceRoot.localScale = Placement.GetDeviceRootScale(parent);

            var offset = CreateChild("Offset", deviceRoot);

            var device = new HapticDevice
            {
                definition = definition,
                deviceRoot = deviceRoot,
                offset = offset,
                pc = CreateReceiverSet("PC", offset, definition.pc),
                quest = CreateReceiverSet("Quest", offset, definition.quest),
            };
            Undo.RegisterCreatedObjectUndo(deviceRoot.gameObject, undoName);
            Undo.RecordObject(setup, undoName);

            setup.devices.Add(device);

            PrefabUtility.RecordPrefabInstancePropertyModifications(setup);
            // UNDO END

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

        /// Add a Local Only Proximity receiver with the node's shape and the receiver's address, flags and tags.
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
    }
}

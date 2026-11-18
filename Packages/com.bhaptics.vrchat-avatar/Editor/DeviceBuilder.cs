using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using VRC.Dynamics;
using VRC.SDK3.Dynamics.Contact.Components;

namespace bHaptics.VRChatAvatar
{
    /// Builds a device's objects from its definition. Records nothing for Undo; HapticsSetupActions does.
    public static class DeviceBuilder
    {
        /// Create the device root, Offset and receiver sets for definition, with the build target's set active.
        /// Returns the new device, not yet recorded for Undo nor added to setup.
        public static HapticDevice CreateDevice(HapticsSetup setup, DeviceDefinition definition)
        {
            var avatarRoot = setup.transform;
            var parent = Placement.GetDeviceRootParent(avatarRoot, definition.bone);

            // STEP 1. Device Root
            var deviceRoot = CreateChild($"bHaptics {definition.displayName}", parent);
            deviceRoot.localPosition = Placement.GetDeviceRootPosition(avatarRoot, parent, definition.position);
            deviceRoot.localRotation = Placement.GetDeviceRootRotation(avatarRoot, parent);
            deviceRoot.localScale = Placement.GetDeviceRootScale(parent);

            // STEP 2. Offset
            var offset = CreateChild("Offset", deviceRoot);

            // STEP 3. Receiver sets and meshes
            var device = new HapticDevice
            {
                definition = definition,
                deviceRoot = deviceRoot,
                offset = offset,
            };
            CreateReceiverSets(device);

            return device;
        }

        /// Create the PC and Quest sets under device's Offset from its definition, with the build target's set active.
        public static void CreateReceiverSets(HapticDevice device)
        {
            device.pc = CreateReceiverSet("PC", device.offset, device.definition.pc);
            device.quest = CreateReceiverSet("Quest", device.offset, device.definition.quest);
            AddMesh(device.pc, device.definition.pcMesh, device.definition.pcMaterial);
            AddMesh(device.quest, device.definition.questMesh, device.definition.questMaterial);
            SetActiveReceiverSet(device, IsBuildTargetMobile());
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

        /// Add a Mesh child to set showing mesh with material. Does nothing when mesh is empty.
        private static void AddMesh(GameObject set, Mesh mesh, Material material)
        {
            if (mesh == null)
            {
                return;
            }

            var child = CreateChild("Mesh", set.transform).gameObject;
            child.AddComponent<MeshFilter>().sharedMesh = mesh;
            child.AddComponent<MeshRenderer>().sharedMaterial = material;
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

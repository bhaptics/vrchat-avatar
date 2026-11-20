using System.Collections.Generic;
using UnityEngine;
using VRC.Dynamics;
using VRC.SDK3.Dynamics.Contact.Components;

namespace bHaptics.VRChatAvatar
{
    /// Builder for device from definition
    public static class DeviceBuilder
    {
        /// Create the device root, Offset, receiver sets and meshes for definition, with the build target's set active.
        /// Returns the new device, not yet recorded for Undo.
        public static HapticDevice CreateDevice(Transform avatarRoot, DeviceDefinition definition)
        {
            var parent = Placement.GetDeviceRootParent(avatarRoot, definition.bone);

            // STEP 1. Device Root
            var deviceRoot = CreateChild($"bHaptics {definition.displayName}", parent);
            deviceRoot.localPosition = Placement.GetDeviceRootPosition(avatarRoot, parent, definition.position);
            deviceRoot.localRotation = Placement.GetDeviceRootRotation(avatarRoot, parent);
            deviceRoot.localScale = Placement.GetDeviceRootScale(parent);

            // STEP 2. Offset
            var offset = CreateChild("Offset", deviceRoot);

            // STEP 3. Receiver sets
            var device = new HapticDevice
            {
                definition = definition,
                deviceRoot = deviceRoot,
                offset = offset,
            };
            CreateReceiverSets(device);

            // STEP 4. Meshes
            AddMeshes(device);

            return device;
        }

        /// Create the PC and Quest receiver sets under device's Offset from its definition, with the build target's set active.
        public static void CreateReceiverSets(HapticDevice device)
        {
            device.pc = CreateReceiverSet("PC", device.offset, device.definition.pc);
            device.quest = CreateReceiverSet("Quest", device.offset, device.definition.quest);
            Platforms.SelectUploadedSet(device, Platforms.IsBuildTargetMobile());
        }

        /// Add each platform's mesh into its receiver set, so the mesh follows the set's active state and EditorOnly tag; shown or hidden as device says.
        public static void AddMeshes(HapticDevice device)
        {
            device.pcMesh = AddMesh(device.pc, device.definition.pcMesh, device.definition.pcMaterial);
            device.questMesh = AddMesh(device.quest, device.definition.questMesh, device.definition.questMaterial);
            ShowMeshes(device);
        }

        /// Show device's meshes, or hide them as set apart: inactive and EditorOnly, so they never upload.
        public static void ShowMeshes(HapticDevice device)
        {
            Platforms.SetUploaded(device.pcMesh, device.showMesh);
            Platforms.SetUploaded(device.questMesh, device.showMesh);
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

        /// Add a Proximity, Local Only receiver to target with node's shape and spec's address, self/others and tags.
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

        /// Add a Mesh child to set showing mesh with material. Returns it, or null when mesh is empty.
        private static GameObject AddMesh(GameObject set, Mesh mesh, Material material)
        {
            if (mesh == null)
            {
                return null;
            }

            var child = CreateChild("Mesh", set.transform).gameObject;
            child.AddComponent<MeshFilter>().sharedMesh = mesh;
            child.AddComponent<MeshRenderer>().sharedMaterial = material;
            return child;
        }
    }
}

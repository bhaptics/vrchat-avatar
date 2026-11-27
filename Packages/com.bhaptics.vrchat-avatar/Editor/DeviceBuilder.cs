using System.Collections.Generic;
using UnityEngine;
using VRC.Dynamics;
using VRC.SDK3.Dynamics.Contact.Components;

namespace bHaptics.VRChatAvatar
{
    /// Builds a device's scene objects from its definition; records nothing for Undo.
    public static class DeviceBuilder
    {
        /// Create the device root, Offset, receiver sets and meshes for definition, with the build target's set active.
        /// Returns the new device, not yet recorded for Undo.
        public static HapticDevice CreateDevice(Transform avatarRoot, DeviceDefinition definition)
        {
            var parent = DevicePlacements.GetDeviceRootParent(avatarRoot, definition);
            var axes = DevicePlacements.GetAxes(avatarRoot, definition, parent);
            var origin = DevicePlacements.GetOrigin(avatarRoot, definition, parent);
            var scale = DevicePlacements.GetScale(avatarRoot, definition);

            var deviceRoot = CreateChild($"bHaptics {definition.displayName}", parent);
            deviceRoot.localPosition = Placement.GetDeviceRootPosition(axes, parent, origin, definition.position * scale);
            deviceRoot.localRotation = Placement.GetDeviceRootRotation(axes, parent);
            deviceRoot.localScale = Placement.GetDeviceRootScale(parent) * scale;

            var offset = CreateChild("Offset", deviceRoot);
            offset.gameObject.AddComponent<DeviceOffset>();

            var device = new HapticDevice
            {
                definition = definition,
                deviceRoot = deviceRoot,
                offset = offset,
            };
            CreateReceiverSets(avatarRoot, device);

            AddMeshes(device);
            AddIndicator(device);

            return device;
        }

        /// Create the PC and Quest receiver sets under device's Offset from its definition, and a glove's fingertip parts, with the build target's active.
        public static void CreateReceiverSets(Transform avatarRoot, HapticDevice device)
        {
            device.pcParts.Clear();
            device.questParts.Clear();
            device.pc = CreateReceiverSet("PC", avatarRoot, device, device.definition.pc, device.pcParts);
            device.quest = CreateReceiverSet("Quest", avatarRoot, device, device.definition.quest, device.questParts);
            Platforms.SelectUploadedSet(device, Platforms.IsBuildTargetMobile());
        }

        /// Add each platform's mesh into its receiver set, so the mesh follows the set's active state and EditorOnly tag; shown or hidden as device says.
        public static void AddMeshes(HapticDevice device)
        {
            device.pcMeshObject = AddMesh(device.pc, device.definition.pcMesh, device.definition.pcMaterial);
            device.questMeshObject = AddMesh(device.quest, device.definition.questMesh, device.definition.questMaterial);
            ApplyShowMesh(device);
        }

        /// Show or hide device's meshes as its showMesh says; hidden ones are inactive and EditorOnly, so they never upload.
        public static void ApplyShowMesh(HapticDevice device)
        {
            Platforms.SetUploaded(device.pcMeshObject, device.showMesh);
            Platforms.SetUploaded(device.questMeshObject, device.showMesh);
        }

        /// Add the indicator into the PC set, so it follows the set's active state and EditorOnly tag; shown or hidden as device says.
        public static void AddIndicator(HapticDevice device)
        {
            device.pcIndicatorObject = CreateIndicator(device.pc, device.definition.pcIndicatorModel, device.definition.pcIndicatorMaterial);
            ApplyShowIndicator(device);
        }

        /// Show or hide device's indicator as its showIndicator says; a hidden one is inactive and EditorOnly, so it never uploads.
        public static void ApplyShowIndicator(HapticDevice device)
        {
            Platforms.SetUploaded(device.pcIndicatorObject, device.showIndicator);
        }

        /// Returns a new empty child of parent with an identity local transform.
        private static Transform CreateChild(string name, Transform parent)
        {
            var child = new GameObject(name).transform;
            child.SetParent(parent, false);
            return child;
        }

        /// Create a child of device's Offset with one node per definition node, each carrying its receivers; a glove's fingertip nodes go to parts instead. Returns the set.
        private static GameObject CreateReceiverSet(string name, Transform avatarRoot, HapticDevice device, DeviceDefinition.Node[] nodes, List<GameObject> parts)
        {
            var set = CreateChild(name, device.offset);
            for (var i = 0; i < nodes.Length; i++)
            {
                var node = nodes[i];
                Transform nodeTransform;
                if (DevicePlacements.TryPlaceGloveNode(avatarRoot, device.definition, i, out var bone, out var position))
                {
                    if (bone != null)
                    {
                        // A fingertip node lives under its finger bone so it follows the curl; it can't sit in the set under the Offset.
                        nodeTransform = CreatePart($"bHaptics {device.definition.displayName} {node.name} {name}", bone, parts);
                        nodeTransform.position = position;
                    }
                    else
                    {
                        // Placed in the device's frame, like a definition's node, so a Reset keeps the user's Offset.
                        nodeTransform = CreateChild(node.name, set);
                        nodeTransform.localPosition = device.deviceRoot.InverseTransformPoint(position);
                    }
                }
                else
                {
                    nodeTransform = CreateChild(node.name, set);
                    nodeTransform.localPosition = node.position;
                    nodeTransform.localEulerAngles = node.rotation;
                }

                foreach (var receiver in node.receivers)
                {
                    AddReceiver(nodeTransform.gameObject, node, receiver);
                }
            }

            return set.gameObject;
        }

        /// Create a part under bone, scaled to metres, and add it to parts. Returns its Transform.
        private static Transform CreatePart(string name, Transform bone, List<GameObject> parts)
        {
            var part = CreateChild(name, bone);
            part.localScale = Placement.GetDeviceRootScale(bone);
            parts.Add(part.gameObject);
            return part;
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

        /// Add a Mesh child to set at its origin, showing mesh with material, so a reset mesh reads 0 0 0. Returns it, or null when mesh is empty.
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

        /// Add an Indicator child to set with model's skinned mesh, its bones rebuilt from the bindposes, so the model's hierarchy doesn't matter.
        /// Returns it, or null when model is empty.
        private static GameObject CreateIndicator(GameObject set, GameObject model, Material material)
        {
            if (model == null)
            {
                return null;
            }

            // The bones are objects of their own: a receiver under a bone scaled to 0 would shrink with it.
            var source = model.GetComponentInChildren<SkinnedMeshRenderer>(true);
            var indicator = CreateChild("Indicator", set.transform);
            var bindposes = source.sharedMesh.bindposes;
            var bones = new Transform[bindposes.Length];
            for (var i = 0; i < bones.Length; i++)
            {
                var pose = bindposes[i].inverse;
                bones[i] = CreateChild(source.bones[i].name, indicator);
                bones[i].localPosition = pose.GetColumn(3);
                bones[i].localRotation = pose.rotation;
                bones[i].localScale = pose.lossyScale;
            }

            var renderer = indicator.gameObject.AddComponent<SkinnedMeshRenderer>();
            renderer.sharedMesh = source.sharedMesh;
            renderer.bones = bones;
            renderer.rootBone = indicator;
            renderer.localBounds = source.sharedMesh.bounds;
            renderer.sharedMaterial = material;
            return indicator.gameObject;
        }
    }
}

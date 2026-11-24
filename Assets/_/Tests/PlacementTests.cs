using NUnit.Framework;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace bHaptics.VRChatAvatar.Tests
{
    public class PlacementTests
    {
        private GameObject root;
        private Transform armature;
        private Transform bone;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("Avatar");
            root.transform.rotation = Quaternion.Euler(0f, 30f, 0f);

            armature = new GameObject("Armature").transform;
            armature.SetParent(root.transform, false);
            armature.localRotation = Quaternion.Euler(-90f, 0f, 0f);

            bone = new GameObject("Bone").transform;
            bone.SetParent(armature, false);
            bone.localRotation = Quaternion.Euler(10f, 20f, 5f);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(root);
        }

        [Test]
        public void GetDeviceRootRotation_LinesUpWithRoot()
        {
            var deviceRootRotation = bone.rotation * Placement.GetDeviceRootRotation(root.transform.rotation, bone);

            Assert.That(Quaternion.Angle(deviceRootRotation, root.transform.rotation), Is.LessThan(0.01f));
        }

        [Test]
        public void GetViewPosition_IgnoresRootScale()
        {
            root.transform.position = new Vector3(1f, 0f, 2f);
            root.transform.localScale = Vector3.one * 1.2f;
            root.AddComponent<VRCAvatarDescriptor>().ViewPosition = new Vector3(0f, 1.2f, 0.1f);

            var viewPosition = Placement.GetViewPosition(root.transform);

            // (0, 1.2, 0.1) turned 30° about Y, then moved by the root's position; the root's 1.2 scale doesn't apply.
            Assert.That(Vector3.Distance(viewPosition, new Vector3(1.05f, 1.2f, 2.0866f)), Is.LessThan(0.0001f));
        }

        [Test]
        public void GetDeviceRootPosition_OffsetsFromOriginInRootAxesAndMetres()
        {
            armature.localScale = Vector3.one * 100f;
            var origin = new Vector3(0.3f, 1.4f, -0.2f);
            var position = new Vector3(0f, 0.04f, 0.04f);
            var deviceRoot = new GameObject("Device Root").transform;
            deviceRoot.SetParent(bone, false);

            deviceRoot.localPosition = Placement.GetDeviceRootPosition(root.transform.rotation, bone, origin, position);

            Assert.That(Vector3.Distance(deviceRoot.position, origin + root.transform.rotation * position), Is.LessThan(0.0001f));
        }

        [Test]
        public void GetDeviceRootScale_CancelsParentScale()
        {
            root.transform.localScale = Vector3.one * 1.2f;
            armature.localScale = Vector3.one * 100f;
            var deviceRoot = new GameObject("Device Root").transform;
            deviceRoot.SetParent(bone, false);

            deviceRoot.localScale = Placement.GetDeviceRootScale(bone);

            Assert.That(Vector3.Distance(deviceRoot.lossyScale, Vector3.one), Is.LessThan(0.0001f));
        }
    }
}

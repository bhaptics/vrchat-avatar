using NUnit.Framework;
using UnityEngine;

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
        public void GetAnchorRotation_LinesUpWithRoot()
        {
            var anchorRotation = bone.rotation * Placement.GetAnchorRotation(root.transform, bone);

            Assert.That(Quaternion.Angle(anchorRotation, root.transform.rotation), Is.LessThan(0.01f));
        }

        [Test]
        public void GetAnchorScale_CancelsParentScale()
        {
            root.transform.localScale = Vector3.one * 1.2f;
            armature.localScale = Vector3.one * 100f;
            var anchor = new GameObject("Anchor").transform;
            anchor.SetParent(bone, false);

            anchor.localScale = Placement.GetAnchorScale(bone);

            Assert.That(Vector3.Distance(anchor.lossyScale, Vector3.one), Is.LessThan(0.0001f));
        }
    }
}

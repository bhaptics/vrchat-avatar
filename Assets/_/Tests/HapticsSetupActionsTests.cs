using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using VRC.Dynamics;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Dynamics.Contact.Components;

namespace bHaptics.VRChatAvatar.Tests
{
    /// The actions on a non-Humanoid avatar, so the device root sits at the avatar root; PlacementTests covers the bone math.
    public class HapticsSetupActionsTests
    {
        private const string PrefabPath = "Assets/_/Tests/HapticsSetupActionsTests.prefab";

        private GameObject avatar;
        private HapticsSetup setup;
        private DeviceDefinition definition;

        [SetUp]
        public void SetUp()
        {
            avatar = new GameObject("Avatar");
            avatar.AddComponent<VRCAvatarDescriptor>();
            setup = avatar.AddComponent<HapticsSetup>();
            definition = CreateDefinition();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(avatar);
            Object.DestroyImmediate(definition);
            AssetDatabase.DeleteAsset(PrefabPath);
        }

        [Test]
        public void AddDevice_BuildsRootOffsetAndBothSets()
        {
            var device = HapticsSetupActions.AddDevice(setup, definition);

            Assert.That(setup.devices, Is.EqualTo(new[] { device }));
            Assert.That(device.deviceRoot.name, Is.EqualTo("bHaptics Test"));
            Assert.That(device.deviceRoot.parent, Is.EqualTo(avatar.transform));
            Assert.That(device.offset.parent, Is.EqualTo(device.deviceRoot));
            Assert.That(device.pc.transform.parent, Is.EqualTo(device.offset));
            Assert.That(device.quest.transform.parent, Is.EqualTo(device.offset));
            Assert.That(device.pc.GetComponentsInChildren<VRCContactReceiver>(true).Length, Is.EqualTo(4));
            Assert.That(device.quest.GetComponentsInChildren<VRCContactReceiver>(true).Length, Is.EqualTo(2));
        }

        [Test]
        public void AddDevice_ReceiversFollowTheDefinition()
        {
            var device = HapticsSetupActions.AddDevice(setup, definition);

            var receivers = device.pc.GetComponentsInChildren<VRCContactReceiver>(true);
            var specs = definition.pc.SelectMany(node => node.receivers).ToArray();
            Assert.That(receivers.Select(r => r.parameter), Is.EqualTo(specs.Select(s => s.address)));
            foreach (var receiver in receivers)
            {
                var spec = specs.Single(s => s.address == receiver.parameter);
                Assert.That(receiver.receiverType, Is.EqualTo(ContactReceiver.ReceiverType.Proximity));
                Assert.That(receiver.localOnly, Is.True);
                Assert.That(receiver.allowSelf, Is.EqualTo(spec.allowSelf));
                Assert.That(receiver.allowOthers, Is.EqualTo(spec.allowOthers));
                Assert.That(receiver.collisionTags, Is.EqualTo(spec.tags));
                Assert.That(receiver.shapeType, Is.EqualTo(ContactBase.ShapeType.Sphere));
                Assert.That(receiver.radius, Is.EqualTo(0.05f));
            }
        }

        [Test]
        public void AddDevice_UploadsOnlyTheBuildTargetSet()
        {
            var device = HapticsSetupActions.AddDevice(setup, definition);

            var target = EditorUserBuildSettings.activeBuildTarget;
            var isMobile = target == BuildTarget.Android || target == BuildTarget.iOS;
            var uploaded = isMobile ? device.quest : device.pc;
            var kept = isMobile ? device.pc : device.quest;
            Assert.That(uploaded.activeSelf, Is.True);
            Assert.That(uploaded.tag, Is.EqualTo("Untagged"));
            Assert.That(kept.activeSelf, Is.False);
            Assert.That(kept.tag, Is.EqualTo("EditorOnly"));
        }

        [Test]
        public void AddDevice_UndoRemovesObjectsAndEntry()
        {
            Undo.IncrementCurrentGroup();
            var device = HapticsSetupActions.AddDevice(setup, definition);

            Undo.PerformUndo();

            Assert.That(setup.devices, Is.Empty);
            Assert.That(device.deviceRoot == null, Is.True);
        }

        [Test]
        public void AddDevice_RefusesASecondOfTheSameDefinition()
        {
            HapticsSetupActions.AddDevice(setup, definition);

            LogAssert.Expect(LogType.Warning, new Regex("already on this avatar"));
            var second = HapticsSetupActions.AddDevice(setup, definition);

            Assert.That(second, Is.Null);
            Assert.That(setup.devices.Count, Is.EqualTo(1));
            Assert.That(CountDeviceRoots(), Is.EqualTo(1));
        }

        [Test]
        public void AddDevice_RefusesAPrefabAsset()
        {
            var asset = PrefabUtility.SaveAsPrefabAsset(avatar, PrefabPath);
            var assetSetup = asset.GetComponent<HapticsSetup>();

            LogAssert.Expect(LogType.Warning, new Regex("Open the prefab"));
            var device = HapticsSetupActions.AddDevice(assetSetup, definition);

            Assert.That(device, Is.Null);
            Assert.That(assetSetup.devices, Is.Empty);
            Assert.That(CountDeviceRoots(), Is.EqualTo(0));
        }

        [Test]
        public void AddDevice_RefusesASetupOffTheAvatarRoot()
        {
            var child = new GameObject("Armature");
            child.transform.SetParent(avatar.transform, false);
            var childSetup = child.AddComponent<HapticsSetup>();

            LogAssert.Expect(LogType.Warning, new Regex("on the avatar root"));
            var device = HapticsSetupActions.AddDevice(childSetup, definition);

            Assert.That(device, Is.Null);
            Assert.That(childSetup.devices, Is.Empty);
            Assert.That(CountDeviceRoots(), Is.EqualTo(0));
        }

        /// Two PC nodes and one Quest node, each with a self and an others receiver, and no mesh.
        private static DeviceDefinition CreateDefinition()
        {
            var definition = ScriptableObject.CreateInstance<DeviceDefinition>();
            definition.displayName = "Test";
            definition.productName = "Test Product";
            definition.bone = HumanBodyBones.Head;
            definition.pc = new[] { CreateNode("Node 0", "bOSC/v2/Head/0"), CreateNode("Node 1", "bOSC/v2/Head/1") };
            definition.quest = new[] { CreateNode("Node 0", "bOSC/v2m/Head/0") };
            return definition;
        }

        private static DeviceDefinition.Node CreateNode(string name, string address)
        {
            return new DeviceDefinition.Node
            {
                name = name,
                shape = ContactBase.ShapeType.Sphere,
                radius = 0.05f,
                receivers = new[]
                {
                    new DeviceDefinition.Receiver { address = address + "/self", allowSelf = true, tags = new[] { "Head" } },
                    new DeviceDefinition.Receiver { address = address + "/others", allowOthers = true, tags = new[] { "Hand", "Finger" } },
                },
            };
        }

        /// Count the scene's device roots for the test definition, active or not.
        private static int CountDeviceRoots()
        {
            return Object.FindObjectsOfType<Transform>(true).Count(t => t.name == "bHaptics Test");
        }
    }
}

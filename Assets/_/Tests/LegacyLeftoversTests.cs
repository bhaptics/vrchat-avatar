using NUnit.Framework;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Dynamics.Contact.Components;

namespace bHaptics.VRChatAvatar.Tests
{
    public class LegacyLeftoversTests
    {
        private GameObject avatar;
        private HapticsSetup setup;
        private DeviceDefinition definition;

        [SetUp]
        public void SetUp()
        {
            avatar = new GameObject("Avatar");
            avatar.AddComponent<VRCAvatarDescriptor>();
            setup = avatar.AddComponent<HapticsSetup>();
            definition = IndicatorTestKit.CreateDefinition();
            HapticsSetupActions.AddDevice(setup, definition);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(avatar);
            Object.DestroyImmediate(definition);
        }

        [Test]
        public void TheDevicesReceivers_AreNotForeign()
        {
            Assert.That(LegacyLeftovers.FindForeignReceivers(setup), Is.Empty);
        }

        [TestCase("bOSC/v2/Head/0/self")]
        [TestCase("bOSC_v1_Head_0")]
        [TestCase("bHapticsOSC_Head_0")]
        public void AReceiverOutsideTheDevices_IsForeign(string parameter)
        {
            var receiver = AddReceiver(parameter);

            Assert.That(LegacyLeftovers.FindForeignReceivers(setup), Is.EqualTo(new[] { receiver }));
        }

        [Test]
        public void LeftoversThatDontUpload_AndOtherReceivers_DontCount()
        {
            AddReceiver("bOSC/v2/Head/0/self").gameObject.SetActive(false);
            AddReceiver("bOSC/v2/Head/1/self").gameObject.tag = "EditorOnly";
            AddReceiver("Mine");

            Assert.That(LegacyLeftovers.FindForeignReceivers(setup), Is.Empty);
        }

        private VRCContactReceiver AddReceiver(string parameter)
        {
            var node = new GameObject(parameter);
            node.transform.SetParent(avatar.transform, false);
            var receiver = node.AddComponent<VRCContactReceiver>();
            receiver.parameter = parameter;
            return receiver;
        }
    }
}

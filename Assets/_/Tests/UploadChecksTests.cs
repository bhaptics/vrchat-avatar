using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.TestTools;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Dynamics.Contact.Components;

namespace bHaptics.VRChatAvatar.Tests
{
    public class UploadChecksTests
    {
        private GameObject avatar;

        [SetUp]
        public void SetUp()
        {
            avatar = new GameObject("Avatar");
            avatar.AddComponent<VRCAvatarDescriptor>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(avatar);
        }

        [Test]
        public void AnAvatarWithoutHapticReceivers_IsNeverBlocked()
        {
            AddSenders(UploadChecks.QuestContactLimit + 1);

            Assert.That(UploadChecks.GetProblems(avatar.transform, true), Is.Empty);
        }

        [Test]
        public void PcReceivers_BlockOnlyAQuestUpload()
        {
            AddReceiver("bOSC/v2/Head/0/self", avatar.transform);

            Assert.That(GetKinds(true), Is.EqualTo(new[] { UploadChecks.ProblemKind.WrongSchema }));
            Assert.That(GetKinds(false), Is.Empty);
        }

        [Test]
        public void QuestReceivers_BlockOnlyAPcUpload()
        {
            AddReceiver("bOSC/v2m/Head/0/self", avatar.transform);

            Assert.That(GetKinds(false), Is.EqualTo(new[] { UploadChecks.ProblemKind.WrongSchema }));
            Assert.That(GetKinds(true), Is.Empty);
        }

        [Test]
        public void BothSchemas_BlockEitherUpload()
        {
            AddReceiver("bOSC/v2/Head/0/self", avatar.transform);
            AddReceiver("bOSC/v2m/Head/0/self", avatar.transform);

            Assert.That(GetKinds(false), Is.EqualTo(new[] { UploadChecks.ProblemKind.Mixed }));
            Assert.That(GetKinds(true), Is.EqualTo(new[] { UploadChecks.ProblemKind.Mixed }));
        }

        [Test]
        public void InactiveAndEditorOnlyReceivers_DontCount()
        {
            AddReceiver("bOSC/v2/Head/0/self", avatar.transform);
            var editorOnly = new GameObject("Quest");
            editorOnly.transform.SetParent(avatar.transform, false);
            editorOnly.tag = "EditorOnly";
            AddReceiver("bOSC/v2m/Head/0/self", editorOnly.transform);
            AddReceiver("bOSC/v2m/Head/1/self", avatar.transform).gameObject.SetActive(false);

            Assert.That(GetKinds(false), Is.Empty);
        }

        [Test]
        public void ContactsThatArentLocalOnly_BlockAQuestUploadPastTheLimit()
        {
            AddReceiver("bOSC/v2m/Head/0/self", avatar.transform);
            AddSenders(UploadChecks.QuestContactLimit);
            Assert.That(GetKinds(true), Is.Empty);

            AddSenders(1);
            Assert.That(GetKinds(true), Is.EqualTo(new[] { UploadChecks.ProblemKind.TooManyContacts }));
        }

        [Test]
        public void TheCallback_StopsABlockedBuild()
        {
            AddReceiver("bOSC/v2/Head/0/self", avatar.transform);
            AddReceiver("bOSC/v2m/Head/0/self", avatar.transform);

            LogAssert.Expect(LogType.Error, new Regex(@"\[bHaptics\] Upload blocked: PC \(v2\) and Quest \(v2m\)"));
            Assert.That(new UploadCheckCallback().OnPreprocessAvatar(avatar), Is.False);
        }

        [Test]
        public void TheCallback_LetsAnAvatarWithoutHapticReceiversThrough()
        {
            AddSenders(UploadChecks.QuestContactLimit + 1);

            Assert.That(new UploadCheckCallback().OnPreprocessAvatar(avatar), Is.True);
        }

        [Test]
        public void FxParametersOfTheOtherSchema_Block()
        {
            AddReceiver("bOSC/v2m/Head/0/self", avatar.transform);
            var fx = GiveFxDeclaring("bOSC/v2/Head/0/self");

            try
            {
                Assert.That(GetKinds(true), Is.EqualTo(new[] { UploadChecks.ProblemKind.FxSchema }));
            }
            finally
            {
                Object.DestroyImmediate(fx);
            }
        }

        [Test]
        public void FxParametersOfTheOtherSchema_BlockThroughAnOverrideController()
        {
            AddReceiver("bOSC/v2m/Head/0/self", avatar.transform);
            var fx = GiveFxDeclaring("bOSC/v2/Head/0/self");
            var overrideController = new AnimatorOverrideController(fx);
            avatar.GetComponent<VRCAvatarDescriptor>().baseAnimationLayers[0].animatorController = overrideController;

            try
            {
                Assert.That(GetKinds(true), Is.EqualTo(new[] { UploadChecks.ProblemKind.FxSchema }));
            }
            finally
            {
                Object.DestroyImmediate(overrideController);
                Object.DestroyImmediate(fx);
            }
        }

        [Test]
        public void FxParametersOfTheBuildsSchema_DontBlock()
        {
            AddReceiver("bOSC/v2/Head/0/self", avatar.transform);
            var fx = GiveFxDeclaring("bOSC/v2/Head/0/self");

            try
            {
                Assert.That(GetKinds(false), Is.Empty);
            }
            finally
            {
                Object.DestroyImmediate(fx);
            }
        }

        private UploadChecks.ProblemKind[] GetKinds(bool isMobile)
        {
            return UploadChecks.GetProblems(avatar.transform, isMobile).Select(problem => problem.Kind).ToArray();
        }

        /// Add a Local Only receiver writing parameter, as the package builds them, under parent.
        private static VRCContactReceiver AddReceiver(string parameter, Transform parent)
        {
            var node = new GameObject(parameter.Replace('/', ' '));
            node.transform.SetParent(parent, false);
            var receiver = node.AddComponent<VRCContactReceiver>();
            receiver.parameter = parameter;
            receiver.localOnly = true;
            return receiver;
        }

        /// Put an in-memory FX declaring a Float parameter into the descriptor's FX slot. Returns it, for the test to destroy.
        private AnimatorController GiveFxDeclaring(string parameter)
        {
            var fx = new AnimatorController();
            fx.AddParameter(parameter, AnimatorControllerParameterType.Float);
            var descriptor = avatar.GetComponent<VRCAvatarDescriptor>();
            descriptor.customizeAnimationLayers = true;
            descriptor.baseAnimationLayers = new[] { new VRCAvatarDescriptor.CustomAnimLayer { type = VRCAvatarDescriptor.AnimLayerType.FX, animatorController = fx } };
            return fx;
        }

        /// Add count senders that aren't Local Only, like an avatar's own contacts.
        private void AddSenders(int count)
        {
            for (var i = 0; i < count; i++)
            {
                var node = new GameObject($"Sender {i}");
                node.transform.SetParent(avatar.transform, false);
                node.AddComponent<VRCContactSender>().localOnly = false;
            }
        }
    }
}

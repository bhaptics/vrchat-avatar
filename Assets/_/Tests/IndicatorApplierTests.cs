using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.TestTools;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
using VRC.SDKBase;

namespace bHaptics.VRChatAvatar.Tests
{
    /// Apply with the AAC writer, on a non-Humanoid avatar with one two-node device. Generated assets are deleted after each test.
    public class IndicatorApplierTests
    {
        private const string FxPath = "Assets/_/Tests/IndicatorApplierTests.controller";
        private static readonly string[] Bools = IndicatorTestKit.Bools;

        private GameObject avatar;
        private VRCAvatarDescriptor descriptor;
        private HapticsSetup setup;
        private DeviceDefinition definition;
        private HapticDevice device;
        private string[] generatedBefore;

        [SetUp]
        public void SetUp()
        {
            generatedBefore = GetGenerated();
            avatar = new GameObject("Avatar");
            descriptor = avatar.AddComponent<VRCAvatarDescriptor>();
            setup = avatar.AddComponent<HapticsSetup>();
            definition = IndicatorTestKit.CreateDefinition();
            device = HapticsSetupActions.AddDevice(setup, definition);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(avatar);
            Object.DestroyImmediate(definition);
            foreach (var path in GetGenerated().Except(generatedBefore))
            {
                AssetDatabase.DeleteAsset(path);
            }

            AssetDatabase.DeleteAsset(FxPath);
        }

        [Test]
        public void WithoutFx_CreatesTheFxAndExpressionsFromVRChatsDefaults()
        {
            var plan = IndicatorApplier.Apply(setup, IndicatorPlanner.Plan(setup, false));

            var fx = IndicatorPlanner.FindFx(descriptor);
            Assert.That(plan.blockers, Is.Empty);
            Assert.That(AssetDatabase.GetAssetPath(fx), Does.Match(@"^Assets/bHaptics/Generated/Avatar_FX@bHaptics_\d{8}-\d{6}\.controller$"));
            Assert.That(fx.layers.Select(l => l.name).Where(n => n.StartsWith("bHaptics/")), Is.EqualTo(Bools));
            Assert.That(descriptor.expressionParameters.parameters.Select(p => p.name), Is.EqualTo(new[] { "VRCEmote", "VRCFaceBlendH", "VRCFaceBlendV" }.Concat(Bools)));
            Assert.That(descriptor.expressionsMenu, Is.Not.Null);
            Assert.That(descriptor.expressionsMenu.Parameters, Is.EqualTo(descriptor.expressionParameters));
            Assert.That(IndicatorPlanner.Plan(setup, false).IsUpToDate, Is.True);
        }

        [Test]
        public void WritesOffAndOnStatesThatDriveTheBoolLocally()
        {
            IndicatorApplier.Apply(setup, IndicatorPlanner.Plan(setup, false));

            var machine = IndicatorPlanner.FindFx(descriptor).layers.First(l => l.name == Bools[0]).stateMachine;
            Assert.That(machine.states.Select(s => s.state.name), Is.EqualTo(new[] { "Off", "On" }));
            Assert.That(machine.defaultState.name, Is.EqualTo("Off"));
            var driver = (VRCAvatarParameterDriver)machine.states[1].state.behaviours.Single();
            Assert.That(driver.localOnly, Is.True);
            Assert.That(driver.parameters.Single().name, Is.EqualTo(Bools[0]));
            Assert.That(driver.parameters.Single().value, Is.EqualTo(1f));
        }

        [Test]
        public void OnLightsTheBoneAtItsOwnScale()
        {
            var bone = device.pcIndicatorObject.GetComponent<SkinnedMeshRenderer>().bones.First(b => b.name == "Head_0");
            bone.localScale = Vector3.one * 2f;

            IndicatorApplier.Apply(setup, IndicatorPlanner.Plan(setup, false));

            var machine = IndicatorPlanner.FindFx(descriptor).layers.First(l => l.name == Bools[0]).stateMachine;
            var on = (AnimationClip)machine.states.First(s => s.state.name == "On").state.motion;
            var binding = AnimationUtility.GetCurveBindings(on).First(b => b.propertyName == "m_LocalScale.x");
            Assert.That(AnimationUtility.GetEditorCurve(on, binding).keys[0].value, Is.EqualTo(2f));
        }

        [Test]
        public void CopiesTheFx_LeavesTheOriginalAlone()
        {
            var original = AnimatorController.CreateAnimatorControllerAtPath(FxPath);
            original.AddParameter("Mine", AnimatorControllerParameterType.Float);
            var originalAssets = AssetDatabase.LoadAllAssetsAtPath(FxPath).Length;
            GiveFx(original);

            IndicatorApplier.Apply(setup, IndicatorPlanner.Plan(setup, false));

            var fx = IndicatorPlanner.FindFx(descriptor);
            Assert.That(fx, Is.Not.EqualTo(original));
            Assert.That(fx.name, Does.StartWith("IndicatorApplierTests@bHaptics_"));
            Assert.That(fx.layers.Length, Is.EqualTo(3));
            Assert.That(original.layers.Length, Is.EqualTo(1));
            Assert.That(original.parameters.Select(p => p.name), Is.EqualTo(new[] { "Mine" }));
            Assert.That(AssetDatabase.LoadAllAssetsAtPath(FxPath).Length, Is.EqualTo(originalAssets));
        }

        [Test]
        public void FollowsTheControllersWriteDefaults()
        {
            var original = AnimatorController.CreateAnimatorControllerAtPath(FxPath);
            original.layers[0].stateMachine.AddState("Mine").writeDefaultValues = true;
            GiveFx(original);

            IndicatorApplier.Apply(setup, IndicatorPlanner.Plan(setup, false));

            var states = IndicatorPlanner.FindFx(descriptor).layers.Where(l => l.name.StartsWith("bHaptics/")).SelectMany(l => l.stateMachine.states);
            Assert.That(states.Select(s => s.state.writeDefaultValues), Has.All.True);
        }

        [Test]
        public void ApplyingAgain_KeepsTheBoolPositionsAndDoesntGrow()
        {
            IndicatorApplier.Apply(setup, IndicatorPlanner.Plan(setup, false));
            var first = IndicatorPlanner.FindFx(descriptor);
            var names = descriptor.expressionParameters.parameters.Select(p => p.name).ToArray();
            var assets = AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(first)).Length;
            // A renamed indicator moves its bone paths, so the layers are written again.
            device.pcIndicatorObject.name = "Lights";

            var plan = IndicatorApplier.Apply(setup, IndicatorPlanner.Plan(setup, false));

            var second = IndicatorPlanner.FindFx(descriptor);
            Assert.That(plan.layersToReplace, Is.EquivalentTo(Bools));
            Assert.That(second, Is.Not.EqualTo(first));
            Assert.That(Regex.Matches(second.name, "@bHaptics_").Count, Is.EqualTo(1));
            Assert.That(AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(second)).Length, Is.EqualTo(assets));
            Assert.That(descriptor.expressionParameters.parameters.Select(p => p.name), Is.EqualTo(names));
        }

        [Test]
        public void OnQuest_RemovesTheLayersAndReceiverParameters_KeepsTheBools()
        {
            IndicatorApplier.Apply(setup, IndicatorPlanner.Plan(setup, false));
            var names = descriptor.expressionParameters.parameters.Select(p => p.name).ToArray();

            IndicatorApplier.Apply(setup, IndicatorPlanner.Plan(setup, true));

            var fx = IndicatorPlanner.FindFx(descriptor);
            Assert.That(fx.layers.Select(l => l.name), Has.None.StartWith("bHaptics/"));
            Assert.That(fx.parameters.Select(p => p.name), Has.None.StartWith("bOSC/"));
            Assert.That(descriptor.expressionParameters.parameters.Select(p => p.name), Is.EqualTo(names));
        }

        [Test]
        public void UndoPutsTheSlotsBack()
        {
            Undo.IncrementCurrentGroup();
            IndicatorApplier.Apply(setup, IndicatorPlanner.Plan(setup, false));

            Undo.PerformUndo();

            Assert.That(IndicatorPlanner.FindFx(descriptor), Is.Null);
            Assert.That(descriptor.customExpressions, Is.False);
        }

        [Test]
        public void WhenBlocked_ChangesNothing()
        {
            Object.DestroyImmediate(device.pcIndicatorObject);

            LogAssert.Expect(LogType.Warning, new Regex("weren't applied"));
            var plan = IndicatorApplier.Apply(setup, IndicatorPlanner.Plan(setup, false));

            Assert.That(plan.blockers, Is.Not.Empty);
            Assert.That(IndicatorPlanner.FindFx(descriptor), Is.Null);
            Assert.That(GetGenerated().Except(generatedBefore), Is.Empty);
        }

        [Test]
        public void RemoveSetup_TakesOursOutOfNewCopies_LeavesTheAppliedOnesAlone()
        {
            IndicatorApplier.Apply(setup, IndicatorPlanner.Plan(setup, false));
            var applied = IndicatorPlanner.FindFx(descriptor);
            var appliedParameters = descriptor.expressionParameters;

            HapticsSetupActions.RemoveSetup(setup);

            var fx = IndicatorPlanner.FindFx(descriptor);
            Assert.That(fx, Is.Not.EqualTo(applied));
            Assert.That(fx.layers.Select(l => l.name), Has.None.StartWith("bHaptics/"));
            Assert.That(fx.parameters.Select(p => p.name), Has.None.StartWith("bOSC/").And.None.StartWith("bHaptics/"));
            Assert.That(descriptor.expressionParameters.parameters.Select(p => p.name), Has.None.StartWith("bHaptics/"));
            Assert.That(applied.layers.Select(l => l.name).Where(n => n.StartsWith("bHaptics/")), Is.EqualTo(Bools));
            Assert.That(appliedParameters.parameters.Select(p => p.name).Where(n => n.StartsWith("bHaptics/")), Is.EqualTo(Bools));
        }

        [Test]
        public void RemoveSetup_WithNothingApplied_CopiesNothing()
        {
            HapticsSetupActions.RemoveSetup(setup);

            Assert.That(IndicatorPlanner.FindFx(descriptor), Is.Null);
            Assert.That(GetGenerated().Except(generatedBefore), Is.Empty);
        }

        [Test]
        public void RemoveSetup_UndoPutsTheAppliedSlotsBack()
        {
            IndicatorApplier.Apply(setup, IndicatorPlanner.Plan(setup, false));
            var applied = IndicatorPlanner.FindFx(descriptor);
            var appliedParameters = descriptor.expressionParameters;
            Undo.IncrementCurrentGroup();
            HapticsSetupActions.RemoveSetup(setup);

            Undo.PerformUndo();

            Assert.That(avatar.GetComponent<HapticsSetup>() != null, Is.True);
            Assert.That(IndicatorPlanner.FindFx(descriptor), Is.EqualTo(applied));
            Assert.That(descriptor.expressionParameters, Is.EqualTo(appliedParameters));
        }

        [Test]
        public void LegacyFx_LosesItsLayersAndTheirClips_AndItsBoolReceiversBecomeFloats()
        {
            var original = AnimatorController.CreateAnimatorControllerAtPath(FxPath);
            original.AddParameter("bOSC/v2/Head/0/self", AnimatorControllerParameterType.Bool);
            original.AddParameter("bOSC_v1_Head_0", AnimatorControllerParameterType.Float);
            var legacyOnly = AddClip(original, "zAutogenerated__key__Head 0");
            var shared = AddClip(original, "zAutogenerated__key__Shared");
            original.AddLayer("bHapticsOSC__Head/0");
            var legacy = original.layers.Last().stateMachine;
            legacy.AddState("Off").motion = legacyOnly;
            legacy.AddState("Shared").motion = shared;
            original.layers[0].stateMachine.AddState("Mine").motion = shared;
            GiveFx(original);

            IndicatorApplier.Apply(setup, IndicatorPlanner.Plan(setup, false));

            var fx = IndicatorPlanner.FindFx(descriptor);
            var clips = AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(fx)).OfType<AnimationClip>().Select(c => c.name).ToList();
            Assert.That(fx.layers.Select(l => l.name), Has.None.StartWith("bHapticsOSC__"));
            Assert.That(clips, Does.Not.Contain("zAutogenerated__key__Head 0").And.Contain("zAutogenerated__key__Shared"));
            Assert.That(fx.parameters.Single(p => p.name == "bOSC/v2/Head/0/self").type, Is.EqualTo(AnimatorControllerParameterType.Float));
            Assert.That(fx.parameters.Select(p => p.name), Has.None.StartWith("bOSC_v1_"));
            Assert.That(IndicatorPlanner.Plan(setup, false).IsUpToDate, Is.True);
            Assert.That(original.layers.Length, Is.EqualTo(2));
        }

        private static AnimationClip AddClip(AnimatorController controller, string name)
        {
            var clip = new AnimationClip { name = name };
            AssetDatabase.AddObjectToAsset(clip, controller);
            return clip;
        }

        private void GiveFx(AnimatorController fx)
        {
            descriptor.customizeAnimationLayers = true;
            descriptor.baseAnimationLayers = new[] { new VRCAvatarDescriptor.CustomAnimLayer { type = VRCAvatarDescriptor.AnimLayerType.FX, animatorController = fx } };
        }

        private static string[] GetGenerated()
        {
            return AssetDatabase.IsValidFolder(GeneratedAssets.Folder)
                ? AssetDatabase.FindAssets("", new[] { GeneratedAssets.Folder }).Select(AssetDatabase.GUIDToAssetPath).ToArray()
                : new string[0];
        }
    }
}

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
        private const string ParametersPath = "Assets/_/Tests/IndicatorApplierTests.asset";
        private static readonly string[] Bools = IndicatorTestKit.Bools;

        private GameObject avatar;
        private VRCAvatarDescriptor descriptor;
        private HapticsSetup setup;
        private DeviceDefinition definition;
        private HapticDevice device;
        private string[] generatedBefore;
        private int undoGroup;

        [SetUp]
        public void SetUp()
        {
            Undo.IncrementCurrentGroup();
            undoGroup = Undo.GetCurrentGroup();
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
            // The runner reverts Undo after the run, and undoing a destroyed component brings back its avatar, which is gone by then; revert while it's here.
            // A new record first drops the redo a final PerformUndo leaves, which Ctrl+Y would replay as orphans; the revert takes it out too.
            Undo.RegisterCompleteObjectUndo(avatar, "Flush Redo");
            Undo.RevertAllDownToGroup(undoGroup);
            Object.DestroyImmediate(avatar);
            Object.DestroyImmediate(definition);
            foreach (var path in GetGenerated().Except(generatedBefore))
            {
                AssetDatabase.DeleteAsset(path);
            }

            AssetDatabase.DeleteAsset(FxPath);
            AssetDatabase.DeleteAsset(ParametersPath);
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
            Assert.That(descriptor.expressionParameters.parameters.Skip(3).Select(p => p.saved), Has.All.False);
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
        public void OnLightsTheBoneAtItsBindposeScale()
        {
            // A model whose bindposes aren't unit scale, and a bone a playing FX has scaled to 0.
            var renderer = device.pcIndicatorObject.GetComponent<SkinnedMeshRenderer>();
            var mesh = Object.Instantiate(renderer.sharedMesh);
            mesh.bindposes = mesh.bindposes.Select(p => Matrix4x4.Scale(Vector3.one * 0.5f) * p).ToArray();
            renderer.sharedMesh = mesh;
            renderer.bones.First(b => b.name == "Head_0").localScale = Vector3.zero;
            try
            {
                IndicatorApplier.Apply(setup, IndicatorPlanner.Plan(setup, false));

                var machine = IndicatorPlanner.FindFx(descriptor).layers.First(l => l.name == Bools[0]).stateMachine;
                var on = (AnimationClip)machine.states.First(s => s.state.name == "On").state.motion;
                var binding = AnimationUtility.GetCurveBindings(on).First(b => b.propertyName == "m_LocalScale.x");
                Assert.That(AnimationUtility.GetEditorCurve(on, binding).keys[0].value, Is.EqualTo(2f).Within(0.0001f));
            }
            finally
            {
                Object.DestroyImmediate(mesh);
            }
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

        [TestCase(AnimatorControllerParameterType.Float, AnimatorConditionMode.Greater, AnimatorConditionMode.Less)]
        [TestCase(AnimatorControllerParameterType.Int, AnimatorConditionMode.Equals, AnimatorConditionMode.NotEqual)]
        public void FollowsTheTypeIsLocalIsDeclaredWith(AnimatorControllerParameterType type, AnimatorConditionMode local, AnimatorConditionMode remote)
        {
            var original = AnimatorController.CreateAnimatorControllerAtPath(FxPath);
            original.AddParameter(IndicatorPlanner.IsLocal, type);
            GiveFx(original);

            IndicatorApplier.Apply(setup, IndicatorPlanner.Plan(setup, false));

            var fx = IndicatorPlanner.FindFx(descriptor);
            var modes = fx.layers.Where(l => l.name.StartsWith("bHaptics/")).SelectMany(l => l.stateMachine.states)
                .SelectMany(s => s.state.transitions).SelectMany(t => t.conditions)
                .Where(c => c.parameter == IndicatorPlanner.IsLocal).Select(c => c.mode).Distinct();
            Assert.That(modes, Is.EquivalentTo(new[] { local, remote }));
            Assert.That(fx.parameters.Single(p => p.name == IndicatorPlanner.IsLocal).type, Is.EqualTo(type));
            Assert.That(IndicatorPlanner.Plan(setup, false).IsUpToDate, Is.True);
        }

        [Test]
        public void OurBoolsOfAnotherTypeOrUnsynced_AreFixedWhereTheyStand()
        {
            var original = ScriptableObject.CreateInstance<VRCExpressionParameters>();
            original.parameters = new[]
            {
                new VRCExpressionParameters.Parameter { name = "Mine", valueType = VRCExpressionParameters.ValueType.Float, networkSynced = true },
                new VRCExpressionParameters.Parameter { name = Bools[0], valueType = VRCExpressionParameters.ValueType.Int, networkSynced = true, bitWidth = 8, saved = true, defaultValue = 3f },
                new VRCExpressionParameters.Parameter { name = Bools[1], valueType = VRCExpressionParameters.ValueType.Bool, networkSynced = false, saved = true, defaultValue = 1f },
            };
            AssetDatabase.CreateAsset(original, ParametersPath);
            descriptor.customExpressions = true;
            descriptor.expressionParameters = original;

            var plan = IndicatorApplier.Apply(setup, IndicatorPlanner.Plan(setup, false));

            var parameters = descriptor.expressionParameters.parameters;
            Assert.That(parameters.Select(p => p.name), Is.EqualTo(new[] { "Mine" }.Concat(Bools)));
            Assert.That(parameters.Skip(1).Select(p => p.valueType), Has.All.EqualTo(VRCExpressionParameters.ValueType.Bool));
            Assert.That(parameters.Skip(1).Select(p => p.networkSynced), Has.All.True);
            Assert.That(parameters.Skip(1).Select(p => p.saved), Has.All.False);
            Assert.That(parameters.Skip(1).Select(p => p.defaultValue), Has.All.EqualTo(0f));
            Assert.That(descriptor.expressionParameters.CalcTotalCost(), Is.EqualTo(plan.bitsAfter));
            Assert.That(IndicatorPlanner.Plan(setup, false).IsUpToDate, Is.True);
            Assert.That(original.parameters[1].valueType, Is.EqualTo(VRCExpressionParameters.ValueType.Int));
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
        public void UndoThenRedo_LeavesTheAppliedCopyWhole()
        {
            Undo.IncrementCurrentGroup();
            IndicatorApplier.Apply(setup, IndicatorPlanner.Plan(setup, false));
            var copy = IndicatorPlanner.FindFx(descriptor);

            Undo.PerformUndo();
            Undo.PerformRedo();

            var fx = IndicatorPlanner.FindFx(descriptor);
            Assert.That(fx, Is.EqualTo(copy));
            Assert.That(fx.layers.Select(l => l.name).Where(n => n.StartsWith("bHaptics/")), Is.EqualTo(Bools));
            Assert.That(fx.layers.SelectMany(l => l.stateMachine.states).Select(s => s.state.motion), Has.All.Not.Null);
            Assert.That(IndicatorPlanner.Plan(setup, false).IsUpToDate, Is.True);
        }

        [Test]
        public void Undo_LeavesTheCopyAsWritten()
        {
            Undo.IncrementCurrentGroup();
            IndicatorApplier.Apply(setup, IndicatorPlanner.Plan(setup, false));
            var copy = IndicatorPlanner.FindFx(descriptor);

            Undo.PerformUndo();

            // The slot goes back, but the copy, saved as a file, keeps what Apply wrote.
            Assert.That(copy.layers.Select(l => l.name).Where(n => n.StartsWith("bHaptics/")), Is.EqualTo(Bools));
            Assert.That(copy.parameters.Select(p => p.name), Does.Contain("bOSC/v2/Head/0/self"));
        }

        [Test]
        public void UndoAfterApplyingAgain_LeavesTheNewCopyWhole()
        {
            IndicatorApplier.Apply(setup, IndicatorPlanner.Plan(setup, false));
            device.pcIndicatorObject.name = "Lights";
            Undo.IncrementCurrentGroup();
            IndicatorApplier.Apply(setup, IndicatorPlanner.Plan(setup, false));
            var second = IndicatorPlanner.FindFx(descriptor);

            Undo.PerformUndo();

            // The slot goes back to the first copy; the second, which replaced our layers, keeps them whole.
            Assert.That(IndicatorPlanner.FindFx(descriptor), Is.Not.EqualTo(second));
            Assert.That(second.layers.Select(l => l.name).Where(n => n.StartsWith("bHaptics/")), Is.EqualTo(Bools));
            Assert.That(second.layers.SelectMany(l => l.stateMachine.states).Select(s => s.state.motion), Has.All.Not.Null);
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
            var tree = AddTree(original, "Legacy Tree", legacyOnly);
            tree.AddChild(AddTree(original, "Legacy Nested Tree", legacyOnly));
            var sharedTree = AddTree(original, "Shared Tree", shared);
            legacy.AddState("Tree").motion = tree;
            legacy.AddState("Shared Tree").motion = sharedTree;
            legacy.AddStateMachine("Legacy Machine").AddState("Legacy Sub");
            legacy.AddAnyStateTransition(legacy.states[0].state).name = "Legacy Any";
            original.layers[0].stateMachine.AddState("Mine").motion = shared;
            original.layers[0].stateMachine.AddState("Mine Tree").motion = sharedTree;
            GiveFx(original);

            IndicatorApplier.Apply(setup, IndicatorPlanner.Plan(setup, false));

            var fx = IndicatorPlanner.FindFx(descriptor);
            var clips = AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(fx)).OfType<AnimationClip>().Select(c => c.name).ToList();
            Assert.That(fx.layers.Select(l => l.name), Has.None.StartWith("bHapticsOSC__"));
            Assert.That(clips, Does.Not.Contain("zAutogenerated__key__Head 0").And.Contain("zAutogenerated__key__Shared"));
            Assert.That(fx.parameters.Single(p => p.name == "bOSC/v2/Head/0/self").type, Is.EqualTo(AnimatorControllerParameterType.Float));
            Assert.That(fx.parameters.Select(p => p.name), Has.None.StartWith("bOSC_v1_"));
            var assets = AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(fx)).Where(a => a != null).ToList();
            Assert.That(assets.Select(a => a.name).Intersect(new[] { "Legacy Tree", "Legacy Nested Tree", "Legacy Machine", "Legacy Sub", "Legacy Any" }), Is.Empty);
            var mineTree = fx.layers[0].stateMachine.states.Single(s => s.state.name == "Mine Tree").state.motion;
            Assert.That(mineTree != null && mineTree.name == "Shared Tree" && assets.Contains(mineTree), Is.True);
            Assert.That(IndicatorPlanner.Plan(setup, false).IsUpToDate, Is.True);
            Assert.That(original.layers.Length, Is.EqualTo(2));
        }

        [Test]
        public void RemoveSetup_WithAnOverrideController_TakesOurBoolsOutAndLeavesTheFx()
        {
            var fx = new AnimatorOverrideController();
            descriptor.customizeAnimationLayers = true;
            descriptor.baseAnimationLayers = new[] { new VRCAvatarDescriptor.CustomAnimLayer { type = VRCAvatarDescriptor.AnimLayerType.FX, animatorController = fx } };
            var original = ScriptableObject.CreateInstance<VRCExpressionParameters>();
            original.parameters = new[] { "Mine" }.Concat(Bools)
                .Select(name => new VRCExpressionParameters.Parameter { name = name, valueType = VRCExpressionParameters.ValueType.Bool, networkSynced = true }).ToArray();
            AssetDatabase.CreateAsset(original, ParametersPath);
            descriptor.customExpressions = true;
            descriptor.expressionParameters = original;
            try
            {
                HapticsSetupActions.RemoveSetup(setup);

                Assert.That(descriptor.baseAnimationLayers[0].animatorController, Is.EqualTo(fx));
                Assert.That(descriptor.expressionParameters.parameters.Select(p => p.name), Is.EqualTo(new[] { "Mine" }));
                Assert.That(original.parameters.Length, Is.EqualTo(3));
            }
            finally
            {
                Object.DestroyImmediate(fx);
            }
        }

        [Test]
        public void ABrokenLayerOfOurs_IsRewrittenByApply()
        {
            GiveFx(AnimatorController.CreateAnimatorControllerAtPath(FxPath));
            IndicatorApplier.Apply(setup, IndicatorPlanner.Plan(setup, false));
            var copy = IndicatorPlanner.FindFx(descriptor);
            var layers = copy.layers;
            layers.First(l => l.name == Bools[0]).stateMachine = null;
            copy.layers = layers;
            EditorUtility.SetDirty(copy);
            AssetDatabase.SaveAssets();

            var plan = IndicatorApplier.Apply(setup, IndicatorPlanner.Plan(setup, false));

            Assert.That(plan.layersToReplace, Does.Contain(Bools[0]));
            Assert.That(IndicatorPlanner.Plan(setup, false).IsUpToDate, Is.True);
        }

        [Test]
        public void RemoveSetup_FromParametersWithoutAMenu_CopiesNoMenu()
        {
            var original = ScriptableObject.CreateInstance<VRCExpressionParameters>();
            original.parameters = new[] { "Mine" }.Concat(Bools)
                .Select(name => new VRCExpressionParameters.Parameter { name = name, valueType = VRCExpressionParameters.ValueType.Bool, networkSynced = true }).ToArray();
            AssetDatabase.CreateAsset(original, ParametersPath);
            descriptor.customExpressions = true;
            descriptor.expressionParameters = original;

            HapticsSetupActions.RemoveSetup(setup);

            Assert.That(descriptor.expressionParameters.parameters.Select(p => p.name), Is.EqualTo(new[] { "Mine" }));
            Assert.That(descriptor.expressionsMenu == null, Is.True);
        }

        private static BlendTree AddTree(AnimatorController controller, string name, Motion child)
        {
            var tree = new BlendTree { name = name };
            AssetDatabase.AddObjectToAsset(tree, controller);
            tree.AddChild(child);
            return tree;
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

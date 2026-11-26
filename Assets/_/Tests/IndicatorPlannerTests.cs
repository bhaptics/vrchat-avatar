using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace bHaptics.VRChatAvatar.Tests
{
    /// The planner on a non-Humanoid avatar with one two-node device; the FX layers are built by hand the way Apply writes them.
    public class IndicatorPlannerTests
    {
        private static readonly string[] Bools = IndicatorTestKit.Bools;

        private GameObject avatar;
        private VRCAvatarDescriptor descriptor;
        private HapticsSetup setup;
        private DeviceDefinition definition;
        private HapticDevice device;

        [SetUp]
        public void SetUp()
        {
            avatar = new GameObject("Avatar");
            descriptor = avatar.AddComponent<VRCAvatarDescriptor>();
            setup = avatar.AddComponent<HapticsSetup>();
            definition = IndicatorTestKit.CreateDefinition();
            device = HapticsSetupActions.AddDevice(setup, definition);
        }

        [TearDown]
        public void TearDown()
        {
            // In-memory assets the tests gave the descriptor.
            Object.DestroyImmediate(IndicatorPlanner.FindFx(descriptor));
            Object.DestroyImmediate(descriptor.expressionParameters);
            Object.DestroyImmediate(avatar);
            Object.DestroyImmediate(definition);
        }

        [Test]
        public void WithoutFx_PlansEveryLayerAndBool()
        {
            var plan = IndicatorPlanner.Plan(setup, false);

            Assert.That(plan.fx, Is.Null);
            Assert.That(plan.layersToAdd, Is.EqualTo(Bools));
            Assert.That(plan.boolsToAdd, Is.EqualTo(Bools));
            // VRChat's defaults: VRCEmote (Int) and two Floats.
            Assert.That(plan.bitsBefore, Is.EqualTo(24));
            Assert.That(plan.bitsAfter, Is.EqualTo(26));
            Assert.That(plan.layers.Select(l => l.bonePath), Is.EqualTo(new[] { "bHaptics Test/Offset/PC/Indicator/Head_0", "bHaptics Test/Offset/PC/Indicator/Head_1" }));
            Assert.That(plan.blockers, Is.Empty);
        }

        [Test]
        public void AfterApply_IsUpToDate()
        {
            GiveAppliedFxAndParameters();

            var plan = IndicatorPlanner.Plan(setup, false);

            Assert.That(plan.IsUpToDate, Is.True);
            Assert.That(plan.bitsAfter, Is.EqualTo(plan.bitsBefore));
        }

        [Test]
        public void OnQuest_RemovesLayersAndReceiverParameters_KeepsBools()
        {
            GiveAppliedFxAndParameters();

            var plan = IndicatorPlanner.Plan(setup, true);

            Assert.That(plan.layers, Is.Empty);
            Assert.That(plan.layersToRemove, Is.EquivalentTo(Bools));
            Assert.That(plan.fxParametersToRemove, Does.Contain("bOSC/v2/Head/0/self").And.Contain("bHaptics/Head/1"));
            Assert.That(plan.ChangesParameters, Is.False);
        }

        [Test]
        public void RenamedIndicator_ReplacesItsLayers()
        {
            GiveAppliedFxAndParameters();
            device.pcIndicatorObject.name = "Lights";

            var plan = IndicatorPlanner.Plan(setup, false);

            Assert.That(plan.layersToReplace, Is.EquivalentTo(Bools));
            Assert.That(plan.ChangesParameters, Is.False);
        }

        [Test]
        public void HiddenIndicator_RemovesItsLayersAndBools()
        {
            GiveAppliedFxAndParameters();
            HapticsSetupActions.SetIndicatorShown(setup, definition, false);

            var plan = IndicatorPlanner.Plan(setup, false);

            Assert.That(plan.layersToRemove, Is.EquivalentTo(Bools));
            Assert.That(plan.boolsToRemove, Is.EquivalentTo(Bools));
            Assert.That(plan.bitsAfter, Is.EqualTo(plan.bitsBefore - 2));
        }

        [Test]
        public void PlanRemoval_TakesEverythingOfOursOut()
        {
            GiveAppliedFxAndParameters();

            var plan = IndicatorPlanner.PlanRemoval(setup);

            Assert.That(plan.layers, Is.Empty);
            Assert.That(plan.bools, Is.Empty);
            Assert.That(plan.layersToRemove, Is.EquivalentTo(Bools));
            Assert.That(plan.boolsToRemove, Is.EquivalentTo(Bools));
            Assert.That(plan.fxParametersToRemove, Does.Contain("bOSC/v2/Head/1/others").And.Contain("bHaptics/Head/0"));
        }

        [Test]
        public void LegacyFx_DeletesItsLayersAndOldPrefixes_AndRetypesBoolReceivers()
        {
            var fx = new AnimatorController();
            fx.AddLayer("bHapticsOSC__Head/0");
            fx.AddParameter("bOSC/v2/Head/0/self", AnimatorControllerParameterType.Bool);
            fx.AddParameter("bOSC_v1_Head_0", AnimatorControllerParameterType.Float);
            fx.AddParameter("bHapticsOSC_Head_0", AnimatorControllerParameterType.Bool);
            descriptor.customizeAnimationLayers = true;
            descriptor.baseAnimationLayers = new[] { new VRCAvatarDescriptor.CustomAnimLayer { type = VRCAvatarDescriptor.AnimLayerType.FX, animatorController = fx } };

            var plan = IndicatorPlanner.Plan(setup, false);

            Assert.That(plan.legacyLayersToRemove, Is.EqualTo(new[] { "bHapticsOSC__Head/0" }));
            Assert.That(plan.fxParametersToRetype, Is.EqualTo(new[] { "bOSC/v2/Head/0/self" }));
            Assert.That(plan.fxParametersToRemove, Is.EquivalentTo(new[] { "bOSC_v1_Head_0", "bHapticsOSC_Head_0" }));
        }

        [Test]
        public void MissingIndicator_Blocks()
        {
            Object.DestroyImmediate(device.pcIndicatorObject);

            var plan = IndicatorPlanner.Plan(setup, false);

            Assert.That(plan.blockers, Has.Some.Contains("indicator is missing"));
        }

        [Test]
        public void PastTheBitLimit_Blocks()
        {
            var parameters = ScriptableObject.CreateInstance<VRCExpressionParameters>();
            // 255 bits of the avatar's own: 31 synced Ints and 7 synced Bools.
            parameters.parameters = Enumerable.Range(0, 31).Select(i => Parameter($"Int{i}", VRCExpressionParameters.ValueType.Int))
                .Concat(Enumerable.Range(0, 7).Select(i => Parameter($"Bool{i}", VRCExpressionParameters.ValueType.Bool))).ToArray();
            descriptor.customExpressions = true;
            descriptor.expressionParameters = parameters;

            var plan = IndicatorPlanner.Plan(setup, false);

            Assert.That(plan.bitsBefore, Is.EqualTo(255));
            Assert.That(plan.bitsAfter, Is.EqualTo(257));
            Assert.That(plan.blockers, Has.Some.Contains("257 of 256"));
        }

        [Test]
        public void IndicatorsWithoutTheirLayers_UploadLit()
        {
            Assert.That(IndicatorPlanner.Plan(setup, false).LeavesIndicatorsLit, Is.True);

            GiveAppliedFxAndParameters();

            Assert.That(IndicatorPlanner.Plan(setup, false).LeavesIndicatorsLit, Is.False);
            // Quest builds no indicator objects, so nothing uploads lit there.
            Assert.That(IndicatorPlanner.Plan(setup, true).LeavesIndicatorsLit, Is.False);
        }

        [Test]
        public void ParametersAUserLayerReads_StayForThisBuildsSchema()
        {
            var fx = new AnimatorController();
            fx.AddParameter("bOSC/v2/Head/0/self", AnimatorControllerParameterType.Float);
            fx.AddParameter("bOSC/v2/Head/1/self", AnimatorControllerParameterType.Float);
            fx.AddParameter("bOSC_v1_Head_0", AnimatorControllerParameterType.Float);
            fx.AddLayer("Mine");
            var machine = fx.layers[0].stateMachine;
            var blush = machine.AddState("Idle").AddTransition(machine.AddState("Blush"));
            blush.AddCondition(AnimatorConditionMode.Greater, 0.1f, "bOSC/v2/Head/0/self");
            blush.AddCondition(AnimatorConditionMode.Greater, 0.1f, "bOSC_v1_Head_0");
            descriptor.customizeAnimationLayers = true;
            descriptor.baseAnimationLayers = new[] { new VRCAvatarDescriptor.CustomAnimLayer { type = VRCAvatarDescriptor.AnimLayerType.FX, animatorController = fx } };
            HapticsSetupActions.SetIndicatorShown(setup, definition, false);

            var pc = IndicatorPlanner.Plan(setup, false);
            var quest = IndicatorPlanner.Plan(setup, true);

            // The legacy schema goes whoever reads it; a receiver of the other platform's schema goes too.
            Assert.That(pc.fxParametersToRemove, Is.EquivalentTo(new[] { "bOSC/v2/Head/1/self", "bOSC_v1_Head_0" }));
            Assert.That(quest.fxParametersToRemove, Is.EquivalentTo(new[] { "bOSC/v2/Head/0/self", "bOSC/v2/Head/1/self", "bOSC_v1_Head_0" }));
        }

        [Test]
        public void OurBoolsOfAnotherTypeOrUnsynced_AreFixed()
        {
            var parameters = ScriptableObject.CreateInstance<VRCExpressionParameters>();
            parameters.parameters = new[]
            {
                Parameter(Bools[0], VRCExpressionParameters.ValueType.Int),
                new VRCExpressionParameters.Parameter { name = Bools[1], valueType = VRCExpressionParameters.ValueType.Bool, networkSynced = false },
            };
            descriptor.customExpressions = true;
            descriptor.expressionParameters = parameters;

            var plan = IndicatorPlanner.Plan(setup, false);

            Assert.That(plan.boolsToFix, Is.EquivalentTo(Bools));
            Assert.That(plan.boolsToAdd, Is.Empty);
            // A synced Int (8 bits) becomes a Bool (1), and the unsynced Bool starts costing 1.
            Assert.That(plan.bitsBefore, Is.EqualTo(8));
            Assert.That(plan.bitsAfter, Is.EqualTo(2));
        }

        [Test]
        public void ABrokenLayerOfOurs_IsReplaced()
        {
            GiveAppliedFxAndParameters();
            var fx = IndicatorPlanner.FindFx(descriptor);
            var layers = fx.layers;
            layers[1].stateMachine = null;
            fx.layers = layers;
            // A transition lost from the file loads as null.
            Object.DestroyImmediate(layers[0].stateMachine.states[0].state.transitions[0]);

            var plan = IndicatorPlanner.Plan(setup, false);

            Assert.That(plan.layersToReplace, Does.Contain(layers[1].name));
            Assert.That(plan.layersToReplace, Does.Contain(layers[0].name));
        }

        [Test]
        public void AnOverrideControllerInTheFxSlot_Blocks()
        {
            var fx = new AnimatorOverrideController();
            descriptor.customizeAnimationLayers = true;
            descriptor.baseAnimationLayers = new[] { new VRCAvatarDescriptor.CustomAnimLayer { type = VRCAvatarDescriptor.AnimLayerType.FX, animatorController = fx } };
            try
            {
                var plan = IndicatorPlanner.Plan(setup, false);

                Assert.That(plan.blockers, Has.Some.Contains("Override Controller"));
                Assert.That(plan.ChangesFx, Is.False);
                Assert.That(plan.LeavesIndicatorsLit, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(fx);
            }
        }

        [Test]
        public void PlanRemoval_WithAnOverrideController_StillTakesOurBoolsOut()
        {
            var fx = new AnimatorOverrideController();
            descriptor.customizeAnimationLayers = true;
            descriptor.baseAnimationLayers = new[] { new VRCAvatarDescriptor.CustomAnimLayer { type = VRCAvatarDescriptor.AnimLayerType.FX, animatorController = fx } };
            var parameters = ScriptableObject.CreateInstance<VRCExpressionParameters>();
            parameters.parameters = Bools.Select(name => Parameter(name, VRCExpressionParameters.ValueType.Bool)).ToArray();
            descriptor.customExpressions = true;
            descriptor.expressionParameters = parameters;
            try
            {
                var plan = IndicatorPlanner.PlanRemoval(setup);

                Assert.That(plan.blockers, Is.Empty);
                Assert.That(plan.ChangesFx, Is.False);
                Assert.That(plan.boolsToRemove, Is.EquivalentTo(Bools));
            }
            finally
            {
                Object.DestroyImmediate(fx);
            }
        }

        [Test]
        public void ClipsForAnotherScale_AreReplaced()
        {
            GiveAppliedFxAndParameters();
            var layers = IndicatorPlanner.Plan(setup, false).layers;
            var fx = IndicatorPlanner.FindFx(descriptor);
            // On lit at another size, and Off that doesn't hide.
            fx.layers.First(l => l.name == Bools[0]).stateMachine.states.First(s => s.state.name == "On").state.motion = ScaleClip(layers.First(l => l.name == Bools[0]).bonePath, 2f);
            fx.layers.First(l => l.name == Bools[1]).stateMachine.states.First(s => s.state.name == "Off").state.motion = ScaleClip(layers.First(l => l.name == Bools[1]).bonePath, 1f);

            var plan = IndicatorPlanner.Plan(setup, false);

            Assert.That(plan.layersToReplace, Is.EquivalentTo(Bools));
        }

        [Test]
        public void AnIndicatorWithoutItsMeshOrBindposes_IsMissing()
        {
            var renderer = device.pcIndicatorObject.GetComponent<SkinnedMeshRenderer>();
            var mesh = Object.Instantiate(renderer.sharedMesh);
            mesh.bindposes = mesh.bindposes.Take(1).ToArray();
            try
            {
                foreach (var broken in new[] { null, mesh })
                {
                    renderer.sharedMesh = broken;
                    Assert.That(IndicatorPlanner.Plan(setup, false).blockers, Has.Some.Contains("indicator is missing"));
                }
            }
            finally
            {
                Object.DestroyImmediate(mesh);
            }
        }

        [Test]
        public void BonesThePlayingFxScaled_StayUpToDate()
        {
            GiveAppliedFxAndParameters();
            // In Play Mode the Off clips scale the bones to 0.
            foreach (var bone in device.pcIndicatorObject.GetComponent<SkinnedMeshRenderer>().bones)
            {
                bone.localScale = Vector3.zero;
            }

            Assert.That(IndicatorPlanner.Plan(setup, false).IsUpToDate, Is.True);
        }

        [Test]
        public void PastTheBitLimitAlready_StillLetsOursGo()
        {
            var parameters = ScriptableObject.CreateInstance<VRCExpressionParameters>();
            // 258 bits: 32 synced Ints of the avatar's own, and our two Bools.
            parameters.parameters = Enumerable.Range(0, 32).Select(i => Parameter($"Int{i}", VRCExpressionParameters.ValueType.Int))
                .Concat(Bools.Select(name => Parameter(name, VRCExpressionParameters.ValueType.Bool))).ToArray();
            descriptor.customExpressions = true;
            descriptor.expressionParameters = parameters;

            var removal = IndicatorPlanner.PlanRemoval(setup);
            var apply = IndicatorPlanner.Plan(setup, false);

            Assert.That(removal.bitsAfter, Is.EqualTo(256));
            Assert.That(removal.blockers, Is.Empty);
            Assert.That(apply.bitsAfter, Is.EqualTo(258));
            Assert.That(apply.blockers, Is.Empty);
        }

        private static VRCExpressionParameters.Parameter Parameter(string name, VRCExpressionParameters.ValueType type)
        {
            return new VRCExpressionParameters.Parameter { name = name, valueType = type, networkSynced = true };
        }

        /// Put into the descriptor an in-memory FX holding the layers as Apply writes them, and parameters declaring the Bools.
        private void GiveAppliedFxAndParameters()
        {
            var fx = new AnimatorController();
            fx.AddParameter(IndicatorPlanner.IsLocal, AnimatorControllerParameterType.Bool);
            foreach (var layer in IndicatorPlanner.Plan(setup, false).layers)
            {
                fx.AddParameter(layer.name, AnimatorControllerParameterType.Bool);
                foreach (var receiver in layer.receivers)
                {
                    fx.AddParameter(receiver, AnimatorControllerParameterType.Float);
                }

                fx.AddLayer(layer.name);
                var machine = fx.layers.Last().stateMachine;
                var off = machine.AddState("Off");
                var on = machine.AddState("On");
                off.motion = ScaleClip(layer.bonePath, 0f);
                on.motion = ScaleClip(layer.bonePath, 1f);
                foreach (var receiver in layer.receivers)
                {
                    var lit = off.AddTransition(on);
                    lit.AddCondition(AnimatorConditionMode.If, 0f, IndicatorPlanner.IsLocal);
                    lit.AddCondition(AnimatorConditionMode.Greater, 0.01f, receiver);
                }

                var remote = off.AddTransition(on);
                remote.AddCondition(AnimatorConditionMode.IfNot, 0f, IndicatorPlanner.IsLocal);
                remote.AddCondition(AnimatorConditionMode.If, 0f, layer.name);
            }

            descriptor.customizeAnimationLayers = true;
            descriptor.baseAnimationLayers = new[] { new VRCAvatarDescriptor.CustomAnimLayer { type = VRCAvatarDescriptor.AnimLayerType.FX, animatorController = fx } };
            var parameters = ScriptableObject.CreateInstance<VRCExpressionParameters>();
            parameters.parameters = Bools.Select(name => Parameter(name, VRCExpressionParameters.ValueType.Bool)).ToArray();
            descriptor.customExpressions = true;
            descriptor.expressionParameters = parameters;
        }

        private static AnimationClip ScaleClip(string path, float scale)
        {
            var clip = new AnimationClip();
            foreach (var axis in new[] { "x", "y", "z" })
            {
                AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, typeof(Transform), $"m_LocalScale.{axis}"), AnimationCurve.Constant(0f, 0f, scale));
            }

            return clip;
        }
    }
}

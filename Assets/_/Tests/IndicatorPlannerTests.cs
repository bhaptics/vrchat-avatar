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

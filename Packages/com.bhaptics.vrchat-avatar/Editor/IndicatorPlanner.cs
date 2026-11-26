using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace bHaptics.VRChatAvatar
{
    /// Compares the avatar's FX and Expression Parameters with what its indicator settings need; touches nothing.
    /// The FX is reconciled, not recorded: a layer is ours by its name and current when it reads the right parameters and bone.
    public static class IndicatorPlanner
    {
        public const string Prefix = "bHaptics/"; // our layers and synced Bools
        public const string IsLocal = "IsLocal";
        private const string ReceiverPrefix = "bOSC/";
        public const string LegacyLayerPrefix = "bHapticsOSC__"; // the legacy package's indicator layers
        // Parameter prefixes that are ours to clean: the contract, our Bools, and the legacy schemas the Players must not see.
        private static readonly string[] OwnedPrefixes = { ReceiverPrefix, Prefix, "bOSC_v1_", "bHapticsOSC_" };
        // VRChat's default expressions, which an avatar without its own uses; Apply starts from copies so the emote menu stays.
        public const string DefaultParametersPath = "Packages/com.vrchat.avatars/Samples/AV3 Demo Assets/Expressions Menu/DefaultExpressionParameters.asset";
        public const string DefaultMenuPath = "Packages/com.vrchat.avatars/Samples/AV3 Demo Assets/Expressions Menu/DefaultExpressionsMenu.asset";

        /// Plan Apply for setup on the build target's platform. Quest shows no indicators for now, but declares the same Bools, so both builds sync them by position.
        public static IndicatorPlan Plan(HapticsSetup setup, bool isMobile)
        {
            return Plan(setup, isMobile, false);
        }

        /// Plan taking every bHaptics layer, FX parameter and Bool out, as Remove bHaptics from Avatar does.
        public static IndicatorPlan PlanRemoval(HapticsSetup setup)
        {
            return Plan(setup, Platforms.IsBuildTargetMobile(), true);
        }

        private static IndicatorPlan Plan(HapticsSetup setup, bool isMobile, bool removing)
        {
            var plan = new IndicatorPlan();
            var descriptor = setup.GetComponent<VRCAvatarDescriptor>();
            if (descriptor == null)
            {
                plan.blockers.Add("Put bHaptics Setup on the avatar root, next to its VRC Avatar Descriptor.");
                return plan;
            }

            var slot = FindFxSlot(descriptor);
            plan.fx = slot as AnimatorController;
            // An override controller has no layers of its own to copy and edit, so its FX is left alone: Apply refuses, Remove still cleans the parameters.
            plan.fxUneditable = slot != null && plan.fx == null;
            if (plan.fxUneditable && !removing)
            {
                plan.blockers.Add("The FX slot holds an Animator Override Controller, which Apply can't edit. Use an Animator Controller there.");
            }

            plan.parameters = descriptor.customExpressions ? descriptor.expressionParameters : null;

            var bools = plan.bools;
            foreach (var device in setup.devices)
            {
                if (removing || !HasIndicator(setup, device))
                {
                    continue;
                }

                var layers = GetLayers(setup.transform, device);
                if (layers == null)
                {
                    plan.blockers.Add($"{device.definition.displayName}'s indicator is missing. Reset it, or turn Show Indicator off.");
                    continue;
                }

                bools.AddRange(layers.Select(layer => layer.name));
                if (!isMobile)
                {
                    plan.layers.AddRange(layers);
                }
            }

            if (!plan.fxUneditable)
            {
                PlanFx(plan, isMobile);
            }

            PlanBools(plan, bools);
            // Apply copies VRChat's default menu when it adds Bools to an avatar without a menu of its own; removing or fixing them needs none.
            if (plan.boolsToAdd.Count > 0 && (!descriptor.customExpressions || descriptor.expressionsMenu == null)
                && AssetDatabase.LoadAssetAtPath<VRCExpressionsMenu>(DefaultMenuPath) == null)
            {
                plan.blockers.Add($"VRChat's default Expressions Menu isn't at {DefaultMenuPath}. Give the avatar a menu of its own.");
            }

            return plan;
        }

        /// Returns the Animator Controller in descriptor's FX slot, or null when the slot is left at VRChat's default or holds something else.
        public static AnimatorController FindFx(VRCAvatarDescriptor descriptor)
        {
            return FindFxSlot(descriptor) as AnimatorController;
        }

        /// Returns whatever controller descriptor's FX slot holds, or null when it's left at VRChat's default.
        private static RuntimeAnimatorController FindFxSlot(VRCAvatarDescriptor descriptor)
        {
            if (!descriptor.customizeAnimationLayers)
            {
                return null;
            }

            foreach (var layer in descriptor.baseAnimationLayers)
            {
                if (layer.type == VRCAvatarDescriptor.AnimLayerType.FX && !layer.isDefault)
                {
                    return layer.animatorController;
                }
            }

            return null;
        }

        private static bool HasIndicator(HapticsSetup setup, HapticDevice device)
        {
            return device.definition != null && device.definition.pcIndicatorModel != null && device.showIndicator && setup.IsOnThisAvatar(device);
        }

        /// Get a layer per PC node of device, its bone matched through the model's bone order. Returns null when the scene indicator doesn't match the model.
        private static List<IndicatorLayer> GetLayers(Transform avatarRoot, HapticDevice device)
        {
            var renderer = device.pcIndicatorObject != null ? device.pcIndicatorObject.GetComponent<SkinnedMeshRenderer>() : null;
            var modelBones = device.definition.pcIndicatorModel.GetComponentInChildren<SkinnedMeshRenderer>(true).bones.Select(b => b.name).ToList();
            if (renderer == null || renderer.sharedMesh == null || renderer.bones.Length != modelBones.Count)
            {
                return null;
            }

            // The bones were built from the bindposes; read their scale there, since a playing FX may have scaled the bones since.
            var bindposes = renderer.sharedMesh.bindposes;
            var layers = new List<IndicatorLayer>();
            foreach (var node in device.definition.pc)
            {
                var index = modelBones.IndexOf(Indicators.GetBoneName(node));
                var bone = index >= 0 ? renderer.bones[index] : null;
                if (bone == null || index >= bindposes.Length || !bone.IsChildOf(avatarRoot))
                {
                    return null;
                }

                layers.Add(new IndicatorLayer
                {
                    name = Indicators.GetParameterName(node),
                    receivers = node.receivers.Select(r => r.address).ToArray(),
                    bone = bone,
                    scale = bindposes[index].inverse.lossyScale,
                    bonePath = AnimationUtility.CalculateTransformPath(bone, avatarRoot),
                });
            }

            return layers;
        }

        private static void PlanFx(IndicatorPlan plan, bool isMobile)
        {
            var existing = plan.fx != null ? plan.fx.layers.Where(l => l.name.StartsWith(Prefix)).ToList() : new List<AnimatorControllerLayer>();
            foreach (var layer in plan.layers)
            {
                var current = existing.Find(l => l.name == layer.name);
                if (current == null)
                {
                    plan.layersToAdd.Add(layer.name);
                }
                else if (!IsCurrent(current, layer))
                {
                    plan.layersToReplace.Add(layer.name);
                }
            }

            plan.layersToRemove.AddRange(existing.Select(l => l.name).Where(name => plan.layers.All(layer => layer.name != name)));
            if (plan.fx == null)
            {
                return;
            }

            plan.legacyLayersToRemove.AddRange(plan.fx.layers.Select(l => l.name).Where(name => name.StartsWith(LegacyLayerPrefix)));

            // Our parameters are the receivers' Floats and the Bools. Whatever the layers don't read goes, so no other schema stays behind;
            // one they read with another type, as the legacy package declared receivers as Bools, goes too and the layers declare it again.
            var needed = new Dictionary<string, AnimatorControllerParameterType>();
            foreach (var layer in plan.layers)
            {
                foreach (var receiver in layer.receivers)
                {
                    needed[receiver] = AnimatorControllerParameterType.Float;
                }

                needed[layer.name] = AnimatorControllerParameterType.Bool;
            }

            // A layer of the user's may read a receiver of this build's schema, or one of our Bools; dropping it would break that layer.
            var buildSchema = isMobile ? "bOSC/v2m/" : "bOSC/v2/";
            var readByOthers = GetParametersUsedBy(plan.fx.layers.Where(l => !l.name.StartsWith(Prefix) && !l.name.StartsWith(LegacyLayerPrefix)));
            foreach (var parameter in plan.fx.parameters.Where(p => OwnedPrefixes.Any(p.name.StartsWith)))
            {
                if (!needed.TryGetValue(parameter.name, out var type))
                {
                    if (readByOthers.Contains(parameter.name) && (parameter.name.StartsWith(buildSchema) || parameter.name.StartsWith(Prefix)))
                    {
                        continue;
                    }

                    plan.fxParametersToRemove.Add(parameter.name);
                }
                else if (parameter.type != type)
                {
                    plan.fxParametersToRetype.Add(parameter.name);
                }
            }
        }

        /// Get the parameters layers read or drive: transition conditions, state and blend tree parameters, and parameter drivers.
        private static HashSet<string> GetParametersUsedBy(IEnumerable<AnimatorControllerLayer> layers)
        {
            var names = new HashSet<string>();
            foreach (var layer in layers)
            {
                AddParametersUsedBy(layer.stateMachine, names);
            }

            return names;
        }

        private static void AddParametersUsedBy(AnimatorStateMachine machine, HashSet<string> names)
        {
            if (machine == null)
            {
                return;
            }

            var states = machine.states.Select(s => s.state).Where(s => s != null).ToList();
            var transitions = machine.anyStateTransitions.Cast<AnimatorTransitionBase>()
                .Concat(machine.entryTransitions)
                .Concat(machine.stateMachines.SelectMany(child => machine.GetStateMachineTransitions(child.stateMachine)))
                .Concat(states.SelectMany(s => s.transitions));
            names.UnionWith(transitions.Where(t => t != null).SelectMany(t => t.conditions).Select(c => c.parameter));
            foreach (var state in states)
            {
                if (state.speedParameterActive)
                {
                    names.Add(state.speedParameter);
                }

                if (state.cycleOffsetParameterActive)
                {
                    names.Add(state.cycleOffsetParameter);
                }

                if (state.mirrorParameterActive)
                {
                    names.Add(state.mirrorParameter);
                }

                if (state.timeParameterActive)
                {
                    names.Add(state.timeParameter);
                }

                AddParametersUsedBy(state.motion, names);
                AddDrivenParameters(state.behaviours, names);
            }

            AddDrivenParameters(machine.behaviours, names);
            foreach (var child in machine.stateMachines)
            {
                AddParametersUsedBy(child.stateMachine, names);
            }
        }

        private static void AddParametersUsedBy(Motion motion, HashSet<string> names)
        {
            var tree = motion as BlendTree;
            if (tree == null)
            {
                return;
            }

            names.Add(tree.blendParameter);
            names.Add(tree.blendParameterY);
            foreach (var child in tree.children)
            {
                names.Add(child.directBlendParameter);
                AddParametersUsedBy(child.motion, names);
            }
        }

        private static void AddDrivenParameters(IEnumerable<StateMachineBehaviour> behaviours, HashSet<string> names)
        {
            foreach (var driver in behaviours.OfType<VRCAvatarParameterDriver>())
            {
                foreach (var parameter in driver.parameters)
                {
                    names.Add(parameter.name);
                    names.Add(parameter.source);
                }
            }
        }

        /// Returns true when current reads exactly the parameters layer needs and animates only its bone.
        private static bool IsCurrent(AnimatorControllerLayer current, IndicatorLayer layer)
        {
            // A broken layer, with no state machine or a lost state or transition, is replaced like a stale one.
            var machine = current.stateMachine;
            if (machine == null || machine.states.Any(s => s.state == null))
            {
                return false;
            }

            var transitions = machine.anyStateTransitions.Concat(machine.states.SelectMany(s => s.state.transitions)).ToList();
            if (transitions.Any(t => t == null))
            {
                return false;
            }

            var parameters = new HashSet<string>(transitions.SelectMany(t => t.conditions).Select(c => c.parameter));
            var expected = new HashSet<string>(layer.receivers.Append(layer.name).Append(IsLocal));

            var paths = new HashSet<string>(machine.states.Select(s => s.state.motion).OfType<AnimationClip>()
                .SelectMany(AnimationUtility.GetCurveBindings).Select(b => b.path));

            // Off keys the bone's scale to 0 and On to its built scale; a clip keying another scale would hide or light it wrongly.
            var off = machine.states.Select(s => s.state).FirstOrDefault(s => s.name == "Off");
            var on = machine.states.Select(s => s.state).FirstOrDefault(s => s.name == "On");
            return parameters.SetEquals(expected) && paths.SetEquals(new[] { layer.bonePath })
                && off != null && KeysScale(off.motion as AnimationClip, layer.bonePath, Vector3.zero)
                && on != null && KeysScale(on.motion as AnimationClip, layer.bonePath, layer.scale);
        }

        /// Returns true when every key clip has for the scale of the Transform at path matches scale.
        private static bool KeysScale(AnimationClip clip, string path, Vector3 scale)
        {
            if (clip == null)
            {
                return false;
            }

            for (var axis = 0; axis < 3; axis++)
            {
                var curve = AnimationUtility.GetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, typeof(Transform), "m_LocalScale." + "xyz"[axis]));
                if (curve == null || curve.keys.Any(k => Mathf.Abs(k.value - scale[axis]) > 0.0001f))
                {
                    return false;
                }
            }

            return true;
        }

        /// Returns the parameters Apply starts from: the slot's, or VRChat's defaults when the avatar has none of its own.
        public static VRCExpressionParameters GetSourceParameters(IndicatorPlan plan)
        {
            return plan.parameters != null ? plan.parameters : AssetDatabase.LoadAssetAtPath<VRCExpressionParameters>(DefaultParametersPath);
        }

        private static void PlanBools(IndicatorPlan plan, List<string> bools)
        {
            var source = GetSourceParameters(plan);
            // The defaults sit in the SDK's samples, which a later SDK may move.
            if (source == null)
            {
                if (bools.Count > 0)
                {
                    plan.blockers.Add($"VRChat's default Expression Parameters aren't at {DefaultParametersPath}. Give the avatar Expression Parameters of its own.");
                }

                return;
            }

            var existing = source.parameters.Select(p => p.name).ToList();
            plan.boolsToAdd.AddRange(bools.Where(name => !existing.Contains(name)));
            // Others never see an indicator whose Bool isn't a synced Bool.
            plan.boolsToFix.AddRange(source.parameters
                .Where(p => bools.Contains(p.name) && (p.valueType != VRCExpressionParameters.ValueType.Bool || !p.networkSynced))
                .Select(p => p.name));
            plan.boolsToRemove.AddRange(existing.Where(name => name.StartsWith(Prefix) && !bools.Contains(name)));

            plan.bitsBefore = source.CalcTotalCost();
            var removedBits = source.parameters.Where(p => plan.boolsToRemove.Contains(p.name) && p.networkSynced).Sum(p => p.GetBitWidthByType());
            var fixedBits = source.parameters.Where(p => plan.boolsToFix.Contains(p.name)).Sum(p => 1 - (p.networkSynced ? p.GetBitWidthByType() : 0));
            plan.bitsAfter = plan.bitsBefore + plan.boolsToAdd.Count - removedBits + fixedBits;
            // Only bits Apply adds are refused: an avatar already past the limit can still have ours taken out.
            if (plan.bitsAfter > VRCExpressionParameters.MAX_PARAMETER_COST && plan.bitsAfter > plan.bitsBefore)
            {
                plan.blockers.Add($"Indicators would take the avatar's synced parameters to {plan.bitsAfter} of {VRCExpressionParameters.MAX_PARAMETER_COST} bits. Turn some indicators off.");
            }
        }
    }
}

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
            return Plan(setup, false, true);
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

            plan.fx = FindFx(descriptor);
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

            PlanFx(plan);
            PlanBools(plan, bools);
            return plan;
        }

        /// Returns the controller in descriptor's FX slot, or null when the slot is left at VRChat's default.
        public static AnimatorController FindFx(VRCAvatarDescriptor descriptor)
        {
            if (!descriptor.customizeAnimationLayers)
            {
                return null;
            }

            foreach (var layer in descriptor.baseAnimationLayers)
            {
                if (layer.type == VRCAvatarDescriptor.AnimLayerType.FX && !layer.isDefault)
                {
                    return layer.animatorController as AnimatorController;
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
            if (renderer == null || renderer.bones.Length != modelBones.Count)
            {
                return null;
            }

            var layers = new List<IndicatorLayer>();
            foreach (var node in device.definition.pc)
            {
                var index = modelBones.IndexOf(Indicators.GetBoneName(node));
                var bone = index >= 0 ? renderer.bones[index] : null;
                if (bone == null || !bone.IsChildOf(avatarRoot))
                {
                    return null;
                }

                layers.Add(new IndicatorLayer
                {
                    name = Indicators.GetParameterName(node),
                    receivers = node.receivers.Select(r => r.address).ToArray(),
                    bone = bone,
                    bonePath = AnimationUtility.CalculateTransformPath(bone, avatarRoot),
                });
            }

            return layers;
        }

        private static void PlanFx(IndicatorPlan plan)
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

            foreach (var parameter in plan.fx.parameters.Where(p => OwnedPrefixes.Any(p.name.StartsWith)))
            {
                if (!needed.TryGetValue(parameter.name, out var type))
                {
                    plan.fxParametersToRemove.Add(parameter.name);
                }
                else if (parameter.type != type)
                {
                    plan.fxParametersToRetype.Add(parameter.name);
                }
            }
        }

        /// Returns true when current reads exactly the parameters layer needs and animates only its bone.
        private static bool IsCurrent(AnimatorControllerLayer current, IndicatorLayer layer)
        {
            var machine = current.stateMachine;
            var transitions = machine.anyStateTransitions.Concat(machine.states.SelectMany(s => s.state.transitions));
            var parameters = new HashSet<string>(transitions.SelectMany(t => t.conditions).Select(c => c.parameter));
            var expected = new HashSet<string>(layer.receivers.Append(layer.name).Append(IsLocal));

            var paths = new HashSet<string>(machine.states.Select(s => s.state.motion).OfType<AnimationClip>()
                .SelectMany(AnimationUtility.GetCurveBindings).Select(b => b.path));

            return parameters.SetEquals(expected) && paths.SetEquals(new[] { layer.bonePath });
        }

        /// Returns the parameters Apply starts from: the slot's, or VRChat's defaults when the avatar has none of its own.
        public static VRCExpressionParameters GetSourceParameters(IndicatorPlan plan)
        {
            return plan.parameters != null ? plan.parameters : AssetDatabase.LoadAssetAtPath<VRCExpressionParameters>(DefaultParametersPath);
        }

        private static void PlanBools(IndicatorPlan plan, List<string> bools)
        {
            var source = GetSourceParameters(plan);
            var existing = source.parameters.Select(p => p.name).ToList();
            plan.boolsToAdd.AddRange(bools.Where(name => !existing.Contains(name)));
            plan.boolsToRemove.AddRange(existing.Where(name => name.StartsWith(Prefix) && !bools.Contains(name)));

            plan.bitsBefore = source.CalcTotalCost();
            var removedBits = source.parameters.Where(p => plan.boolsToRemove.Contains(p.name) && p.networkSynced).Sum(p => p.GetBitWidthByType());
            plan.bitsAfter = plan.bitsBefore + plan.boolsToAdd.Count - removedBits;
            // Only bits Apply adds are refused: an avatar already past the limit can still have ours taken out.
            if (plan.bitsAfter > VRCExpressionParameters.MAX_PARAMETER_COST && plan.bitsAfter > plan.bitsBefore)
            {
                plan.blockers.Add($"Indicators would take the avatar's synced parameters to {plan.bitsAfter} of {VRCExpressionParameters.MAX_PARAMETER_COST} bits. Turn some indicators off.");
            }
        }
    }
}

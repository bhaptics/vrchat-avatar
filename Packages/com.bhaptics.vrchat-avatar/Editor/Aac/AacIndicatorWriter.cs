using System.Collections.Generic;
using System.Linq;
using AnimatorAsCode.V1;
using AnimatorAsCode.V1.VRC;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace bHaptics.VRChatAvatar
{
    /// Writes the indicator layers with Animator As Code. This assembly compiles only with AAC installed, and registers the writer as it loads.
    [InitializeOnLoad]
    internal static class AacIndicatorWriter
    {
        private const float Threshold = 0.01f; // a receiver above this lights the indicator

        static AacIndicatorWriter()
        {
            IndicatorApplier.LayerWriter = Write;
        }

        /// One layer per indicator, Off by default. The wearer lights it from the receivers and drives its synced Bool; everyone else follows the Bool.
        private static void Write(AnimatorController fx, IReadOnlyList<IndicatorLayer> indicators, Transform avatarRoot, bool writeDefaults)
        {
            var aac = AacV1.Create(new AacConfiguration
            {
                SystemName = IndicatorApplier.AssetKey,
                AnimatorRoot = avatarRoot,
                DefaultValueRoot = avatarRoot,
                AssetKey = IndicatorApplier.AssetKey,
                AssetContainer = fx,
                ContainerMode = AacConfiguration.Container.Everything,
                DefaultsProvider = new Defaults(writeDefaults),
            });

            foreach (var indicator in indicators)
            {
                var layer = aac.CreateSupportingArbitraryControllerLayer(fx, indicator.name);
                var isLocal = layer.Av3().IsLocal;
                var shown = layer.BoolParameter(indicator.name);
                var receivers = indicator.receivers.Select(layer.FloatParameter).ToList();

                var off = layer.NewState("Off")
                    .WithAnimation(aac.NewClip($"{indicator.bone.name} Off").Scaling(indicator.bone, Vector3.zero))
                    .Drives(shown, false).DrivingLocally();
                // Lit at the scale the bone was built with from the model's bindpose, which need not be 1.
                var on = layer.NewState("On")
                    .WithAnimation(aac.NewClip($"{indicator.bone.name} On").Scaling(indicator.bone, indicator.scale))
                    .Drives(shown, true).DrivingLocally();

                foreach (var receiver in receivers)
                {
                    off.TransitionsTo(on).When(isLocal.IsTrue()).And(receiver.IsGreaterThan(Threshold));
                }

                var dark = on.TransitionsTo(off).When(isLocal.IsTrue());
                foreach (var receiver in receivers)
                {
                    dark.And(receiver.IsLessThan(Threshold));
                }

                off.TransitionsTo(on).When(isLocal.IsFalse()).And(shown.IsTrue());
                on.TransitionsTo(off).When(isLocal.IsFalse()).And(shown.IsFalse());
            }
        }

        /// Names a layer by its full bHaptics/... name, with no system prefix.
        private class Defaults : AacDefaultsProvider
        {
            public Defaults(bool writeDefaults) : base(writeDefaults)
            {
            }

            public override string ConvertLayerNameWithSuffix(string systemName, string suffix)
            {
                return suffix;
            }
        }
    }
}

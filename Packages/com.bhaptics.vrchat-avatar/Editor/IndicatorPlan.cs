using System.Collections.Generic;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace bHaptics.VRChatAvatar
{
    /// What Apply would change in the avatar's FX and Expression Parameters for its indicator settings. Empty lists mean nothing to do.
    public class IndicatorPlan
    {
        public AnimatorController fx; // the FX slot's controller; null: Apply creates one
        public VRCExpressionParameters parameters; // the slot's asset; null: Apply creates one
        public bool fxUneditable; // the FX slot holds what Apply can't edit, such as an override controller; its FX is left alone
        public readonly List<IndicatorLayer> layers = new(); // every layer the FX should hold, in order
        public readonly List<string> bools = new(); // every synced Bool the parameters should hold, on both platforms
        public readonly List<string> layersToAdd = new();
        public readonly List<string> layersToReplace = new(); // ours, but reading other parameters or bones
        public readonly List<string> layersToRemove = new();
        public readonly List<string> legacyLayersToRemove = new(); // the legacy package's, always removed
        public readonly List<string> fxParametersToRemove = new();
        public readonly List<string> fxParametersToRetype = new(); // read by our layers but declared with another type: removed, then declared again
        public readonly List<string> boolsToAdd = new();
        public readonly List<string> boolsToFix = new(); // ours but of another type or not synced: fixed where they stand, so positions hold
        public readonly List<string> boolsToRemove = new();
        // The SDK refuses parameters without a menu, so adding Bools to an avatar without one copies VRChat's default menu.
        // That menu is empty: it replaces VRChat's default emotes.
        public bool copiesDefaultMenu;
        public int bitsBefore;
        public int bitsAfter;
        public readonly List<string> blockers = new(); // why Apply refuses

        public bool ChangesFx => layersToAdd.Count > 0 || layersToReplace.Count > 0 || layersToRemove.Count > 0 || legacyLayersToRemove.Count > 0
            || fxParametersToRemove.Count > 0 || fxParametersToRetype.Count > 0;
        public bool ChangesParameters => boolsToAdd.Count > 0 || boolsToFix.Count > 0 || boolsToRemove.Count > 0;
        public bool IsUpToDate => !ChangesFx && !ChangesParameters;
        // An indicator without its current layer keeps the scene's full bone scale, so it uploads lit for everyone.
        public bool LeavesIndicatorsLit => layersToAdd.Count > 0 || layersToReplace.Count > 0 || (fxUneditable && layers.Count > 0);
    }

    /// One indicator's FX layer: lit while any of its receivers is touched, for the wearer, and from its synced Bool for everyone else.
    public class IndicatorLayer
    {
        public string name; // the layer and its synced Bool, such as bHaptics/VestFront/12
        public string[] receivers; // the node's receiver parameters
        public Transform bone; // scaled to 0 when off, to scale when on
        public Vector3 scale; // the bone's scale as built from the mesh's bindpose
        public string bonePath; // from the avatar root, as the clips bind it
    }
}

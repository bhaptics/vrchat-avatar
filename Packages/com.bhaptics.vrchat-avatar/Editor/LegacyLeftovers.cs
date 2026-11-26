using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using VRC.SDK3.Dynamics.Contact.Components;

namespace bHaptics.VRChatAvatar
{
    /// What the legacy bHaptics package (v1–v2) left on an avatar or in the project. The inspector warns; nothing here deletes.
    public static class LegacyLeftovers
    {
        private const string FolderGuid = "9b3e57fee7640ff468876b1eff944969"; // Assets/bHapticsOSC, the same in every legacy version
        private static readonly string[] AddressPrefixes = { "bOSC/", "bOSC_v1_", "bHapticsOSC_" };

        /// Get the uploaded receivers on setup's avatar that write a bHaptics address but belong to none of its devices.
        public static List<VRCContactReceiver> FindForeignReceivers(HapticsSetup setup)
        {
            var owned = setup.devices.Where(setup.IsOnThisAvatar)
                .SelectMany(d => d.pcParts.Concat(d.questParts).Where(part => part != null).Select(part => part.transform).Append(d.deviceRoot))
                .Where(root => root != null)
                .ToList();
            return setup.GetComponentsInChildren<VRCContactReceiver>(true)
                .Where(r => r.parameter != null && AddressPrefixes.Any(r.parameter.StartsWith))
                .Where(r => UploadChecks.IsUploaded(r, setup.transform) && !owned.Any(r.transform.IsChildOf))
                .ToList();
        }

        /// Returns the legacy package's folder, wherever it was moved, or null when it's gone.
        public static string FindFolder()
        {
            var path = AssetDatabase.GUIDToAssetPath(FolderGuid);
            return AssetDatabase.IsValidFolder(path) ? path : null;
        }
    }
}

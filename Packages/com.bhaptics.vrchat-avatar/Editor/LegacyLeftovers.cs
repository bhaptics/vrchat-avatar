using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using VRC.SDK3.Dynamics.Contact.Components;

namespace bHaptics.VRChatAvatar
{
    /// What the legacy bHaptics package (1.x–2.x) left on an avatar
    /// or in the project. The inspector warns; nothing here deletes.
    public static class LegacyLeftovers
    {
        // Assets/bHapticsOSC, the same in every legacy version.
        private const string FolderGuid = "9b3e57fee7640ff468876b1eff944969";
        private static readonly string[] AddressPrefixes = { "bOSC/", "bOSC_v1_", "bHapticsOSC_" };

        /// Get the uploaded receivers on setup's avatar that write a bHaptics address
        /// outside its devices' objects. A node the user moved out of its set is
        /// among them: nothing references it, so it can't be told from a leftover.
        public static List<VRCContactReceiver> FindForeignReceivers(HapticsSetup setup)
        {
            var owned = setup.devices.Where(setup.IsOnThisAvatar)
                .SelectMany(d => d.pcParts.Concat(d.questParts).Append(d.pc).Append(d.quest).Where(o => o != null).Select(o => o.transform).Append(d.deviceRoot))
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

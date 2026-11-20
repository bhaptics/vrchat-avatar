using UnityEditor;
using UnityEngine;

namespace bHaptics.VRChatAvatar
{
    /// The build target's platform, and which of a device's receiver sets uploads for it.
    public static class Platforms
    {
        /// Returns true when the build target is Android or iOS, which upload the Quest set (v2m). iOS's schema is still to be confirmed.
        public static bool IsBuildTargetMobile()
        {
            var target = EditorUserBuildSettings.activeBuildTarget;
            return target is BuildTarget.Android or BuildTarget.iOS;
        }

        /// Make the platform's set the one that uploads, and keep the other one inactive and EditorOnly so it never does.
        public static void SelectUploadedSet(HapticDevice device, bool isMobile)
        {
            SetUploaded(device.pc, !isMobile);
            SetUploaded(device.quest, isMobile);
        }

        /// Activate the set and clear its tag, or deactivate it and tag it EditorOnly.
        private static void SetUploaded(GameObject set, bool uploaded)
        {
            set.SetActive(uploaded);
            set.tag = uploaded ? "Untagged" : "EditorOnly";
        }
    }
}

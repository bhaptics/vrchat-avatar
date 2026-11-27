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
        internal static void SelectUploadedSet(HapticDevice device, bool isMobile)
        {
            SetUploaded(device.pc, !isMobile);
            SetUploaded(device.quest, isMobile);
            device.pcParts.ForEach(part => SetUploaded(part, !isMobile));
            device.questParts.ForEach(part => SetUploaded(part, isMobile));
        }

        /// Returns true when each of device's sets and parts that still exists uploads exactly when it's the platform's.
        public static bool MatchesPlatform(HapticDevice device, bool isMobile)
        {
            return Matches(device.pc, !isMobile) && Matches(device.quest, isMobile)
                && device.pcParts.TrueForAll(part => Matches(part, !isMobile))
                && device.questParts.TrueForAll(part => Matches(part, isMobile));
        }

        /// Returns true when some device of setup uploads the other platform's set; Switch Receivers fixes it.
        /// An entry pasted from another avatar is left out: its objects are that avatar's to switch.
        public static bool HasMismatch(HapticsSetup setup, bool isMobile)
        {
            return setup.devices.Exists(device => setup.IsOnThisAvatar(device) && !MatchesPlatform(device, isMobile));
        }

        /// Activate target and clear its tag, or deactivate it and tag it EditorOnly so it never uploads. Used for receiver sets and meshes.
        internal static void SetUploaded(GameObject target, bool uploaded)
        {
            // An object the user deleted stays missing; Reset builds it again.
            if (target == null)
            {
                return;
            }

            target.SetActive(uploaded);
            target.tag = uploaded ? "Untagged" : "EditorOnly";
        }

        /// Returns true when set is missing, or uploads exactly when it should.
        private static bool Matches(GameObject set, bool shouldUpload)
        {
            if (set == null)
            {
                return true;
            }

            return IsUploaded(set) == shouldUpload;
        }

        /// Returns true when set goes into the upload: active and not tagged EditorOnly.
        private static bool IsUploaded(GameObject set)
        {
            return set.activeSelf && !set.CompareTag("EditorOnly");
        }
    }
}

using UnityEngine;

namespace bHaptics.VRChatAvatar
{
    public static class Placement
    {
        /// Returns the bone's Transform, or null when there's no Humanoid Animator or the bone isn't mapped.
        public static Transform FindBone(Transform avatarRoot, HumanBodyBones bone)
        {
            var animator = avatarRoot.GetComponent<Animator>();
            if (animator == null || !animator.isHuman)
            {
                return null;
            }

            return animator.GetBoneTransform(bone);
        }

        /// Returns the bone's Transform, or avatarRoot when FindBone returns null.
        public static Transform GetDeviceRootParent(Transform avatarRoot, HumanBodyBones bone)
        {
            var boneTransform = FindBone(avatarRoot, bone);
            if (boneTransform == null)
            {
                return avatarRoot;
            }

            return boneTransform;
        }

        /// Returns the device root's localRotation under parent so it faces like avatarRoot in the scene.
        public static Quaternion GetDeviceRootRotation(Transform avatarRoot, Transform parent)
        {
            return Quaternion.Inverse(parent.rotation) * avatarRoot.rotation;
        }

        /// Returns 1 / parent.lossyScale per axis, so the device root's world scale is 1 (metres).
        public static Vector3 GetDeviceRootScale(Transform parent)
        {
            var scale = parent.lossyScale;
            return new Vector3(Invert(scale.x), Invert(scale.y), Invert(scale.z));
        }

        /// Returns 1 / value, or 1 for zero.
        private static float Invert(float value)
        {
            if (Mathf.Approximately(value, 0f))
            {
                return 1f;
            }

            return 1f / value;
        }
    }
}

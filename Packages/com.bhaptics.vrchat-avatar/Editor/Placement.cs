using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace bHaptics.VRChatAvatar
{
    public static class Placement
    {
        /// Returns the bone's Transform, or null when there's no Humanoid Animator or the bone isn't mapped.
        public static Transform FindBone(Transform avatarRoot, HumanBodyBones bone)
        {
            if (!IsHumanoid(avatarRoot))
            {
                return null;
            }

            return avatarRoot.GetComponent<Animator>().GetBoneTransform(bone);
        }

        /// Returns true when candidate carries the VRC Avatar Descriptor: the avatar root, where placement measures from.
        public static bool IsAvatarRoot(Transform candidate)
        {
            return candidate.GetComponent<VRCAvatarDescriptor>() != null;
        }

        /// Returns true when avatarRoot's Animator has a Humanoid rig, so devices can go on its bones.
        public static bool IsHumanoid(Transform avatarRoot)
        {
            var animator = avatarRoot.GetComponent<Animator>();
            return animator != null && animator.isHuman;
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

        /// Get the avatar's View Position in world space: avatarRoot's position and rotation apply, its scale doesn't (creators set it unscaled, measured).
        /// avatarRoot must carry the VRC Avatar Descriptor.
        public static Vector3 GetViewPosition(Transform avatarRoot)
        {
            var viewPosition = avatarRoot.GetComponent<VRCAvatarDescriptor>().ViewPosition;
            return avatarRoot.position + avatarRoot.rotation * viewPosition;
        }

        /// Get device root's localPosition under parent, offset from origin (a world point) by position in avatarRoot's axes and metres.
        public static Vector3 GetDeviceRootPosition(Transform avatarRoot, Transform parent, Vector3 origin, Vector3 position)
        {
            return parent.InverseTransformPoint(origin + avatarRoot.rotation * position);
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

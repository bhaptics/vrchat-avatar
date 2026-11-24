using UnityEngine;

namespace bHaptics.VRChatAvatar
{
    /// Where each kind of device goes on an avatar: the rules are code here, the offsets and nodes come from the definition.
    /// Device axes: Head and Vest use the avatar's; Forearm runs along the forearm and Foot along the foot; Hand and Glove use the hand's own.
    public static class DevicePlacements
    {
        // Thumb, index, middle, ring, little; each chain falls back from the distal bone up the finger, as v2's glove layout did.
        private static readonly HumanBodyBones[][] LeftFingers =
        {
            new[] { HumanBodyBones.LeftThumbDistal, HumanBodyBones.LeftThumbIntermediate, HumanBodyBones.LeftThumbProximal },
            new[] { HumanBodyBones.LeftIndexDistal, HumanBodyBones.LeftIndexIntermediate, HumanBodyBones.LeftIndexProximal },
            new[] { HumanBodyBones.LeftMiddleDistal, HumanBodyBones.LeftMiddleIntermediate, HumanBodyBones.LeftMiddleProximal },
            new[] { HumanBodyBones.LeftRingDistal, HumanBodyBones.LeftRingIntermediate, HumanBodyBones.LeftRingProximal },
            new[] { HumanBodyBones.LeftLittleDistal, HumanBodyBones.LeftLittleIntermediate, HumanBodyBones.LeftLittleProximal },
        };

        private static readonly HumanBodyBones[][] RightFingers =
        {
            new[] { HumanBodyBones.RightThumbDistal, HumanBodyBones.RightThumbIntermediate, HumanBodyBones.RightThumbProximal },
            new[] { HumanBodyBones.RightIndexDistal, HumanBodyBones.RightIndexIntermediate, HumanBodyBones.RightIndexProximal },
            new[] { HumanBodyBones.RightMiddleDistal, HumanBodyBones.RightMiddleIntermediate, HumanBodyBones.RightMiddleProximal },
            new[] { HumanBodyBones.RightRingDistal, HumanBodyBones.RightRingIntermediate, HumanBodyBones.RightRingProximal },
            new[] { HumanBodyBones.RightLittleDistal, HumanBodyBones.RightLittleIntermediate, HumanBodyBones.RightLittleProximal },
        };

        // Glove nodes 5–7 (wrist, palm on the thumb side, palm on the outer side) in hand lengths along the finger, palmar and across axes, as in v2.
        private static readonly Vector3[] GloveHandNodes =
        {
            new Vector3(0f, 0f, 0f),
            new Vector3(0.5f, 0.1f, 0.2f),
            new Vector3(0.5f, 0.1f, -0.2f),
        };

        /// Get the Transform the device root goes under: the definition's bone, Spine for a Vest without a Chest, or avatarRoot when neither is mapped.
        public static Transform GetDeviceRootParent(Transform avatarRoot, DeviceDefinition definition)
        {
            var bone = Placement.FindBone(avatarRoot, definition.bone);
            if (bone == null && definition.kind == DeviceKind.Vest)
            {
                bone = Placement.FindBone(avatarRoot, HumanBodyBones.Spine);
            }

            return bone != null ? bone : avatarRoot;
        }

        /// Get the world point the definition's position is measured from: Head's View Position, or the parent bone.
        public static Vector3 GetOrigin(Transform avatarRoot, DeviceDefinition definition, Transform parent)
        {
            return definition.kind == DeviceKind.Head ? Placement.GetViewPosition(avatarRoot) : parent.position;
        }

        /// Get the device's axes in the scene, a world rotation, from bone positions and the avatar root's axes, never from bone axes.
        public static Quaternion GetAxes(Transform avatarRoot, DeviceDefinition definition, Transform parent)
        {
            if (parent == avatarRoot)
            {
                return avatarRoot.rotation;
            }

            var isLeft = IsLeft(definition.bone);
            switch (definition.kind)
            {
                case DeviceKind.Forearm:
                    return GetForearmAxes(avatarRoot, parent, isLeft);
                case DeviceKind.Foot:
                    return GetFootAxes(avatarRoot, parent, isLeft);
                case DeviceKind.Hand:
                case DeviceKind.Glove:
                    return GetHandAxes(avatarRoot, parent, isLeft);
                default:
                    return avatarRoot.rotation;
            }
        }

        /// For node index of a glove, place it from the hand rather than the definition. Returns false for any other device.
        /// bone is the finger bone a fingertip node follows, or null for a node that stays on the hand; position is in world space.
        public static bool TryPlaceGloveNode(Transform avatarRoot, DeviceDefinition definition, int index, out Transform bone, out Vector3 position)
        {
            bone = null;
            position = Vector3.zero;
            if (definition.kind != DeviceKind.Glove)
            {
                return false;
            }

            var hand = Placement.FindBone(avatarRoot, definition.bone);
            if (hand == null)
            {
                position = avatarRoot.position;
                return true;
            }

            var isLeft = IsLeft(definition.bone);
            var fingers = isLeft ? LeftFingers : RightFingers;
            if (index < fingers.Length)
            {
                bone = FindFingerBone(avatarRoot, fingers[index], out var step);
                if (bone == null)
                {
                    Debug.LogWarning($"[bHaptics] The {(isLeft ? "left" : "right")} hand's finger {index} isn't mapped, so {definition.displayName} node {index} sits on the hand.", avatarRoot);
                    position = hand.position;
                    return true;
                }

                position = GetFingertip(bone, step);
                return true;
            }

            var offset = GloveHandNodes[Mathf.Min(index - fingers.Length, GloveHandNodes.Length - 1)];
            position = hand.position;
            if (TryGetHandBasis(avatarRoot, hand, isLeft, out var finger, out var palmar, out var across, out var length))
            {
                position += (finger * offset.x + palmar * offset.y + across * offset.z) * length;
            }

            return true;
        }

        /// Forearm axes: Y from the elbow to the wrist, Z the avatar's forward turned square to it.
        private static Quaternion GetForearmAxes(Transform avatarRoot, Transform lowerArm, bool isLeft)
        {
            var hand = Placement.FindBone(avatarRoot, isLeft ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
            if (hand == null)
            {
                return avatarRoot.rotation;
            }

            var along = (hand.position - lowerArm.position).normalized;
            return Quaternion.LookRotation(GetSquare(avatarRoot.forward, avatarRoot.up, along), along);
        }

        /// Foot axes: Z from the ankle to the toes, Y the avatar's up turned square to it; without toes, the avatar's own axes.
        private static Quaternion GetFootAxes(Transform avatarRoot, Transform foot, bool isLeft)
        {
            var toes = Placement.FindBone(avatarRoot, isLeft ? HumanBodyBones.LeftToes : HumanBodyBones.RightToes);
            if (toes == null)
            {
                return avatarRoot.rotation;
            }

            var along = (toes.position - foot.position).normalized;
            return Quaternion.LookRotation(along, GetSquare(avatarRoot.up, avatarRoot.forward, along));
        }

        /// Hand axes: Z from the wrist towards the fingers, Y out of the back of the hand; without knuckles, the forearm's line.
        private static Quaternion GetHandAxes(Transform avatarRoot, Transform hand, bool isLeft)
        {
            if (TryGetHandBasis(avatarRoot, hand, isLeft, out var finger, out var palmar, out _, out _))
            {
                return Quaternion.LookRotation(finger, -palmar);
            }

            var lowerArm = Placement.FindBone(avatarRoot, isLeft ? HumanBodyBones.LeftLowerArm : HumanBodyBones.RightLowerArm);
            if (lowerArm == null)
            {
                return avatarRoot.rotation;
            }

            var along = (hand.position - lowerArm.position).normalized;
            return Quaternion.LookRotation(along, GetSquare(avatarRoot.up, avatarRoot.forward, along));
        }

        /// The hand's finger, palmar and across directions and its length, from the knuckles as v2 found them (palmar points down in a T-pose).
        private static bool TryGetHandBasis(Transform avatarRoot, Transform hand, bool isLeft,
            out Vector3 finger, out Vector3 palmar, out Vector3 across, out float length)
        {
            finger = palmar = across = Vector3.zero;
            length = 0f;

            // Index to little: the widest pair of mapped knuckles gives the steadiest across axis; the thumb's sits off the knuckle line.
            var fingers = isLeft ? LeftFingers : RightFingers;
            Transform thumbSide = null;
            Transform outerSide = null;
            for (var i = 1; i < fingers.Length; i++)
            {
                var chain = fingers[i];
                var knuckle = Placement.FindBone(avatarRoot, chain[chain.Length - 1]);
                if (knuckle == null)
                {
                    continue;
                }

                if (thumbSide == null)
                {
                    thumbSide = knuckle;
                }
                else
                {
                    outerSide = knuckle;
                }
            }

            if (outerSide == null)
            {
                return false;
            }

            var middle = Placement.FindBone(avatarRoot, isLeft ? HumanBodyBones.LeftMiddleProximal : HumanBodyBones.RightMiddleProximal);
            var ahead = middle != null ? middle : thumbSide;
            length = Vector3.Distance(hand.position, ahead.position);
            if (length <= 0f)
            {
                return false;
            }

            finger = (ahead.position - hand.position).normalized;
            across = (thumbSide.position - outerSide.position).normalized;
            var normal = Vector3.Cross(finger, across);
            if (normal.sqrMagnitude < 0.0001f)
            {
                return false;
            }

            palmar = normal.normalized * (isLeft ? -1f : 1f);
            return true;
        }

        /// Find the first mapped bone of chain; step says how far up the finger it fell back.
        private static Transform FindFingerBone(Transform avatarRoot, HumanBodyBones[] chain, out int step)
        {
            for (step = 0; step < chain.Length; step++)
            {
                var bone = Placement.FindBone(avatarRoot, chain[step]);
                if (bone != null)
                {
                    return bone;
                }
            }

            return null;
        }

        /// Get the fingertip beyond bone, from its child bones when they look like the finger, else 0.8 of a segment along it (v2's rule).
        private static Vector3 GetFingertip(Transform bone, int fallbackSteps)
        {
            var segment = bone.parent != null ? Vector3.Distance(bone.parent.position, bone.position) : 0f;

            var tip = bone;
            for (var level = 0; level <= fallbackSteps && tip.childCount > 0; level++)
            {
                tip = tip.GetChild(0);
            }

            // The first child may be a nail mesh or an accessory rather than the tip.
            if (tip != bone && segment > 0f)
            {
                var reach = Vector3.Distance(bone.position, tip.position);
                if (reach > segment * 0.1f && reach < segment * (fallbackSteps + 2f))
                {
                    return tip.position;
                }
            }

            if (segment > 0f)
            {
                return bone.position + (bone.position - bone.parent.position).normalized * segment * 0.8f;
            }

            return bone.position;
        }

        /// Get preferred turned square to along, or fallback when preferred runs along it.
        private static Vector3 GetSquare(Vector3 preferred, Vector3 fallback, Vector3 along)
        {
            var square = Vector3.ProjectOnPlane(preferred, along);
            if (square.sqrMagnitude < 0.0001f)
            {
                square = Vector3.ProjectOnPlane(fallback, along);
            }

            return square.normalized;
        }

        /// Returns true for the left side's bones.
        private static bool IsLeft(HumanBodyBones bone)
        {
            return bone is HumanBodyBones.LeftLowerArm or HumanBodyBones.LeftHand or HumanBodyBones.LeftFoot;
        }
    }
}

using System;
using UnityEngine;
using VRC.Dynamics;

namespace bHaptics.VRChatAvatar
{
    public class DeviceDefinition : ScriptableObject
    {
        public string displayName; // ex) Head
        public string productName; // ex) TactVisor
        public DeviceKind kind; // picks the placement rules in DevicePlacements
        public HumanBodyBones bone;
        public Vector3 position; // ex) (0, 0.015, 0.055): where the device root, and so the mesh's pivot, sits from its origin (the bone, Head's View Position, a Vest's shoulder line), in the device's axes and metres
        public Node[] pc; // node positions are from the device root, in the device's axes; a glove's are worked out from the hand instead
        public Node[] quest;
        public Mesh pcMesh; // empty: no mesh
        public Material pcMaterial;
        public Mesh questMesh;
        public Material questMaterial;

        [Serializable]
        public class Node
        {
            public string name;
            public Vector3 position;
            public Vector3 rotation;
            public ContactBase.ShapeType shape;
            public float radius;
            public float height; // Capsule only
            public Receiver[] receivers;
        }

        [Serializable]
        public class Receiver
        {
            public string address; // ex) bOSC/v2/Head/0/self
            public bool allowSelf;
            public bool allowOthers;
            public string[] tags;
        }
    }
}

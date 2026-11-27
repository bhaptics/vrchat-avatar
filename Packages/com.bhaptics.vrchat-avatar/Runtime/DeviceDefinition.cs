using System;
using UnityEngine;
using VRC.Dynamics;

namespace bHaptics.VRChatAvatar
{
    public class DeviceDefinition : ScriptableObject
    {
        public string displayName;  // e.g. Head
        public string productName;  // e.g. TactVisor
        public DeviceKind kind;
        public HumanBodyBones bone;
        // Offset of the device root (the mesh's pivot) from its origin, in the device's axes and metres.
        // DevicePlacements.GetOrigin picks the origin per kind.
        public Vector3 position;
        public Node[] pc;
        public Node[] quest;
        public Mesh pcMesh;
        public Material pcMaterial;
        // One SkinnedMeshRenderer in pcMesh's space, with a bone per PC
        // node named by Indicators.GetBoneName; empty: no indicator.
        public GameObject pcIndicatorModel;
        public Material pcIndicatorMaterial;
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
            public float height; // capsule only
            public Receiver[] receivers;
        }

        [Serializable]
        public class Receiver
        {
            public string address; // e.g. bOSC/v2/Head/0/self
            public bool allowSelf;
            public bool allowOthers;
            public string[] tags;
        }
    }
}

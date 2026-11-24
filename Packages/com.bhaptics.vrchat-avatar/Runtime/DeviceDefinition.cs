using System;
using UnityEngine;
using VRC.Dynamics;

namespace bHaptics.VRChatAvatar
{
    public class DeviceDefinition : ScriptableObject
    {
        public string displayName; // ex) Head
        public string productName; // ex) TactVisor
        public HumanBodyBones bone;
        public Vector3 position; // ex) (0, 0.04, 0.04): where the device sits from the bone, or from the View Position, in avatar axes and metres
        public bool fromViewPosition; // measure position from the avatar's View Position instead of the bone (Head); the device still follows the bone
        public Node[] pc;
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

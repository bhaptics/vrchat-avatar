using System;
using UnityEngine;

namespace bHaptics.VRChatAvatar
{
    [Serializable]
    public class HapticDevice
    {
        public DeviceDefinition definition;
        public Transform deviceRoot;
        public Transform offset;
        public GameObject pc;
        public GameObject quest;
        public GameObject pcMesh; // the mesh objects in the sets, not the definition's Mesh assets
        public GameObject questMesh;
        public bool showMesh = true;
    }
}

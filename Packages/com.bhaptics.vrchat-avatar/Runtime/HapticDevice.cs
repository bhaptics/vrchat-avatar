using System;
using System.Collections.Generic;
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
        public GameObject pcMeshObject;
        public GameObject questMeshObject;
        public GameObject pcIndicatorObject;
        public List<GameObject> pcParts = new List<GameObject>();
        public List<GameObject> questParts = new List<GameObject>();
        public bool showMesh = true;
        public bool showIndicator = true;
    }
}

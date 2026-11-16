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
    }
}

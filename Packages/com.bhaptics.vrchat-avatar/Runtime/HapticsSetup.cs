using System;
using System.Collections.Generic;
using UnityEngine;
using VRC.SDKBase;

namespace bHaptics.VRChatAvatar
{
    [DisallowMultipleComponent]
    [AddComponentMenu("bHaptics/bHaptics Setup")]
    [HelpURL("https://bhaptics.notion.site/How-to-play-VRChat-with-bHaptics-1226d5724b8b80229ab9e0001ab70b61")]
    public class HapticsSetup : MonoBehaviour, IEditorOnly
    {
        public List<Device> devices = new();

        [Serializable]
        public class Device
        {
            public DeviceDefinition definition;
            public Transform deviceRoot;
            public Transform offset;
            public GameObject pc;
            public GameObject quest;
        }
    }
}

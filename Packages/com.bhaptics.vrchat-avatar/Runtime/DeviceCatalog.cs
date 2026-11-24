using UnityEngine;

namespace bHaptics.VRChatAvatar
{
    /// The devices the package offers, in Add Device order. It references standalone definition assets and never owns them: their GUIDs and fileIDs are how avatars find them.
    public class DeviceCatalog : ScriptableObject
    {
        public DeviceDefinition[] definitions;
    }
}

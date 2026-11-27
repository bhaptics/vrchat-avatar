using UnityEngine;

namespace bHaptics.VRChatAvatar
{
    /// The devices the package offers, in Add Device order.
    /// Definitions are standalone assets, never sub-assets: avatars find them by GUID and fileID.
    public class DeviceCatalog : ScriptableObject
    {
        public DeviceDefinition[] definitions;
    }
}

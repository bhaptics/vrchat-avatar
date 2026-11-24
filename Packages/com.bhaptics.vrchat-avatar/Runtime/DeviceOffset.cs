using UnityEngine;
using VRC.SDKBase;

namespace bHaptics.VRChatAvatar
{
    /// Marks a device's Offset so a click on its mesh in the Scene view selects the Offset, the one transform meant for adjusting; a second click reaches the mesh.
    /// Editor only: the VRChat SDK strips it from the upload.
    [SelectionBase]
    [DisallowMultipleComponent]
    [AddComponentMenu("")]
    public class DeviceOffset : MonoBehaviour, IEditorOnly
    {
    }
}

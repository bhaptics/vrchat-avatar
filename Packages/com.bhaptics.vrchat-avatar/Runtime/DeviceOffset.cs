using UnityEngine;
using VRC.SDKBase;

namespace bHaptics.VRChatAvatar
{
    /// Makes a Scene view click on the mesh select the Offset, the one transform users adjust.
    /// A second click reaches the mesh.
    [SelectionBase]
    [DisallowMultipleComponent]
    [AddComponentMenu("")]
    public class DeviceOffset : MonoBehaviour, IEditorOnly
    {
    }
}

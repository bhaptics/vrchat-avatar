using UnityEngine;
using VRC.SDKBase;

namespace bHaptics.VRChatAvatar
{
    // Added to the avatar root. Editor-only: VRChat strips IEditorOnly components on upload.
    [DisallowMultipleComponent]
    [AddComponentMenu("bHaptics/bHaptics Setup")]
    public class HapticsSetup : MonoBehaviour, IEditorOnly
    {
        public bool Head = true;

        // What the last Apply created, so the next one replaces it instead of searching the scene.
        [HideInInspector] public GameObject HeadObject;
    }
}

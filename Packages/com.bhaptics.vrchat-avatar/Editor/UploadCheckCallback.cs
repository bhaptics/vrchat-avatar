using UnityEditor;
using UnityEngine;
using VRC.SDKBase.Editor.BuildPipeline;

namespace bHaptics.VRChatAvatar
{
    /// Stops an avatar build that UploadChecks finds a problem with; the SDK calls it on the copy it builds.
    public class UploadCheckCallback : IVRCSDKPreprocessAvatarCallback
    {
        // After VRChat's RemoveAvatarEditorOnly (-1024), so only what uploads is counted.
        public int callbackOrder => -1000;

        public bool OnPreprocessAvatar(GameObject avatarGameObject)
        {
            // VRCFury and NDMF run these callbacks on entering Play Mode too, and VRCFury destroys the avatar when one fails.
            // The checks guard uploads, so Play Mode passes.
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return true;
            }

            var problems = UploadChecks.GetProblems(avatarGameObject.transform, Platforms.IsBuildTargetMobile());
            foreach (var problem in problems)
            {
                Debug.LogError($"[bHaptics] Upload blocked: {problem.Message}", avatarGameObject);
            }

            return problems.Count == 0;
        }
    }
}

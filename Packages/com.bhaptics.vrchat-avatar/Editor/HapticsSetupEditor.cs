using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using VRC.Dynamics;
using VRC.SDK3.Dynamics.Contact.Components;

namespace bHaptics.VRChatAvatar
{
    [CustomEditor(typeof(HapticsSetup))]
    public class HapticsSetupEditor : Editor
    {
        // Head (PC) from the v2.3.0 contract: node positions in the head bone's frame, in metres.
        private static readonly Vector3[] HeadNodes =
        {
            new Vector3(-0.05f, -0.01f, -0.005f),
            new Vector3(-0.02f, -0.01f, -0.005f),
            new Vector3(0.02f, -0.01f, -0.005f),
            new Vector3(0.05f, -0.01f, -0.005f),
        };
        private const float HeadRadius = 0.06f;

        // Your own head and torso always touch your head, so the self receivers leave them out.
        private static readonly string[] OthersTags = { "Head", "Torso", "Hand", "Foot", "Finger" };
        private static readonly string[] SelfTags = { "Hand", "Foot", "Finger" };

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            if (GUILayout.Button("Apply"))
                Apply((HapticsSetup)target);
        }

        private static void Apply(HapticsSetup setup)
        {
            Undo.SetCurrentGroupName("Apply bHaptics");
            Undo.RecordObject(setup, "Apply bHaptics");

            if (setup.HeadObject != null)
                Undo.DestroyObjectImmediate(setup.HeadObject);
            setup.HeadObject = setup.Head ? CreateHead(setup) : null;

            PrefabUtility.RecordPrefabInstancePropertyModifications(setup);
        }

        private static GameObject CreateHead(HapticsSetup setup)
        {
            Animator animator = setup.GetComponent<Animator>();
            Transform bone = (animator != null && animator.isHuman) ? animator.GetBoneTransform(HumanBodyBones.Head) : null;
            if (bone == null)
            {
                Debug.LogError("[bHaptics] Head needs a Humanoid Animator with the Head bone mapped, on the same object as bHaptics Setup.", setup);
                return null;
            }

            var root = new GameObject("bHaptics Head");
            root.transform.SetParent(bone, false);
            // Sizes stay in metres whatever the rig's bone scale (Blender rigs often carry 100).
            root.transform.localScale = Divide(Vector3.one, bone.lossyScale);

            for (int i = 0; i < HeadNodes.Length; i++)
            {
                var node = new GameObject($"Node {i}");
                node.transform.SetParent(root.transform, false);
                node.transform.localPosition = HeadNodes[i];
                AddReceiver(node, $"bOSC/v2/Head/{i}/self", true, SelfTags);
                AddReceiver(node, $"bOSC/v2/Head/{i}/others", false, OthersTags);
            }

            Undo.RegisterCreatedObjectUndo(root, "Apply bHaptics");
            Debug.Log($"[bHaptics] Head: {HeadNodes.Length * 2} receivers under {bone.name}.", root);
            return root;
        }

        private static void AddReceiver(GameObject node, string address, bool self, string[] tags)
        {
            var receiver = node.AddComponent<VRCContactReceiver>();
            receiver.shapeType = ContactBase.ShapeType.Sphere;
            receiver.radius = HeadRadius;
            receiver.receiverType = ContactReceiver.ReceiverType.Proximity;
            receiver.parameter = address;
            receiver.allowSelf = self;
            receiver.allowOthers = !self;
            receiver.collisionTags = new List<string>(tags);
        }

        private static Vector3 Divide(Vector3 a, Vector3 b) => new Vector3(
            Mathf.Approximately(b.x, 0f) ? a.x : a.x / b.x,
            Mathf.Approximately(b.y, 0f) ? a.y : a.y / b.y,
            Mathf.Approximately(b.z, 0f) ? a.z : a.z / b.z);
    }
}

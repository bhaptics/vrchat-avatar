using UnityEditor;
using UnityEngine;
using VRC.Dynamics;

namespace bHaptics.VRChatAvatar.Tests
{
    /// A two-node test device with the shipped Head indicator model, for the planner and Apply tests.
    internal static class IndicatorTestKit
    {
        public static readonly string[] Bools = { "bHaptics/Head/0", "bHaptics/Head/1" };

        public static DeviceDefinition CreateDefinition()
        {
            var definition = ScriptableObject.CreateInstance<DeviceDefinition>();
            definition.displayName = "Test";
            definition.pc = new[] { CreateNode("Node 0", "bOSC/v2/Head/0"), CreateNode("Node 1", "bOSC/v2/Head/1") };
            definition.quest = new[] { CreateNode("Node 0", "bOSC/v2m/Head/0") };
            // The builder and the planner read only the model's bones; Head_0 and Head_1 are among the Head model's four.
            definition.pcIndicatorModel = AssetDatabase.LoadAssetAtPath<GameObject>("Packages/com.bhaptics.vrchat-avatar/Runtime/Meshes/Indicators/HeadIndicator.prefab");
            return definition;
        }

        private static DeviceDefinition.Node CreateNode(string name, string address)
        {
            return new DeviceDefinition.Node
            {
                name = name,
                shape = ContactBase.ShapeType.Sphere,
                radius = 0.05f,
                receivers = new[]
                {
                    new DeviceDefinition.Receiver { address = address + "/self", allowSelf = true, tags = new string[0] },
                    new DeviceDefinition.Receiver { address = address + "/others", allowOthers = true, tags = new string[0] },
                },
            };
        }
    }
}

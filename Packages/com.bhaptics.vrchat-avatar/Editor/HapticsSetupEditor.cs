using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace bHaptics.VRChatAvatar
{
    [CustomEditor(typeof(HapticsSetup))]
    public class HapticsSetupEditor : Editor
    {
        // Default references, set on this script's import settings.
        [SerializeField] private VisualTreeAsset inspectorTree;
        [SerializeField] private VisualTreeAsset deviceRowTree;
        [SerializeField] private DeviceDefinition head;

        private HapticsSetup setup;
        private VisualElement root;

        private DeviceDefinition[] Definitions => new[] { head };

        public override VisualElement CreateInspectorGUI()
        {
            setup = (HapticsSetup)target;
            root = inspectorTree.CloneTree();

            var addDevice = root.Q<Button>("add-device");
            addDevice.clicked += () => ShowAddMenu(addDevice);

            root.RegisterCallback<AttachToPanelEvent>(_ => Subscribe());
            root.RegisterCallback<DetachFromPanelEvent>(_ => Unsubscribe());

            Render();
            return root;
        }

        private void Subscribe()
        {
            Undo.undoRedoPerformed += QueueRender;
            EditorApplication.hierarchyChanged += QueueRender;
        }

        private void Unsubscribe()
        {
            Undo.undoRedoPerformed -= QueueRender;
            EditorApplication.hierarchyChanged -= QueueRender;
        }

        /// Rebuilding inside the event that triggered it would tear down the element still handling it.
        private void QueueRender()
        {
            root.schedule.Execute(Render);
        }

        /// Fill the device list from setup, and enable Add Device while a definition is left to add.
        private void Render()
        {
            var list = root.Q("device-list");
            list.Clear();
            foreach (var device in setup.devices)
            {
                list.Add(CreateRow(device));
            }

            root.Q<Button>("add-device").SetEnabled(GetMissingDefinitions().Count > 0);
        }

        /// Create a row for device: its name, product name and a remove button.
        private VisualElement CreateRow(HapticDevice device)
        {
            var row = deviceRowTree.Instantiate();
            row.Q<Foldout>("title").text = device.definition.displayName;
            row.Q<Label>("subtitle").text = device.definition.productName;

            var remove = row.Q<Button>("remove");
            remove.tooltip = $"Remove {device.definition.displayName}";
            remove.style.backgroundImage = (Texture2D)EditorGUIUtility.IconContent("d_winbtn_win_close").image;
            remove.clicked += () => HapticsSetupActions.RemoveDevice(setup, device);

            return row;
        }

        /// Show a menu of the definitions this avatar doesn't have yet, below anchor.
        private void ShowAddMenu(VisualElement anchor)
        {
            var menu = new GenericMenu();
            foreach (var definition in GetMissingDefinitions())
            {
                var label = new GUIContent($"{definition.displayName}   {definition.productName}");
                menu.AddItem(label, false, () => HapticsSetupActions.AddDevice(setup, definition));
            }

            menu.DropDown(anchor.worldBound);
        }

        /// Get the definitions not on this avatar yet. Returns an empty list when every device is added.
        private List<DeviceDefinition> GetMissingDefinitions()
        {
            var missing = new List<DeviceDefinition>();
            foreach (var definition in Definitions)
            {
                if (!setup.devices.Exists(d => d.definition == definition))
                {
                    missing.Add(definition);
                }
            }

            return missing;
        }
    }
}

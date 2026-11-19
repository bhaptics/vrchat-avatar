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

        /// SessionState key for the definition whose row is open on this setup; survives reselection and domain reload.
        private string OpenRowKey => $"bHaptics.OpenRow.{setup.GetInstanceID()}";

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
                var definition = device.definition;
                var open = SessionState.GetInt(OpenRowKey, 0) == definition.GetInstanceID();
                list.Add(new DeviceRow(deviceRowTree, setup, device, open, isOpen => SetOpenRow(isOpen ? definition : null)));
            }

            root.Q<Button>("add-device").SetEnabled(GetMissingDefinitions().Count > 0);
        }

        /// Open the row for definition and close the others; null closes them all.
        private void SetOpenRow(DeviceDefinition definition)
        {
            SessionState.SetInt(OpenRowKey, definition != null ? definition.GetInstanceID() : 0);
            QueueRender();
        }

        /// Show a menu of the definitions this avatar doesn't have yet, below anchor. The added device's row opens.
        private void ShowAddMenu(VisualElement anchor)
        {
            var menu = new GenericMenu();
            foreach (var definition in GetMissingDefinitions())
            {
                var label = new GUIContent($"{definition.displayName}   {definition.productName}");
                menu.AddItem(label, false, () =>
                {
                    if (HapticsSetupActions.AddDevice(setup, definition) != null)
                    {
                        SetOpenRow(definition);
                    }
                });
            }

            menu.DropDown(anchor.worldBound);
        }

        /// Get the definitions not on this avatar yet. Returns an empty list when every device is added.
        private List<DeviceDefinition> GetMissingDefinitions()
        {
            var missing = new List<DeviceDefinition>();
            foreach (var definition in Definitions)
            {
                if (setup.FindDevice(definition) == null)
                {
                    missing.Add(definition);
                }
            }

            return missing;
        }
    }
}

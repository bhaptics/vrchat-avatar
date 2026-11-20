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

        private readonly Dictionary<DeviceDefinition, DeviceRow> rows = new();

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

            SyncRows();
            return root;
        }

        private void Subscribe()
        {
            Undo.undoRedoPerformed += SyncRows;
            EditorApplication.hierarchyChanged += SyncRows;
        }

        private void Unsubscribe()
        {
            Undo.undoRedoPerformed -= SyncRows;
            EditorApplication.hierarchyChanged -= SyncRows;
        }

        /// Match the rows to setup's devices: keep existing rows, add missing ones, drop removed ones, then refresh them.
        /// Enable Add Device while a definition is left to add; disabled, its tooltip says why.
        private void SyncRows()
        {
            var list = root.Q("device-list");
            var openId = SessionState.GetInt(OpenRowKey, 0);
            for (var i = 0; i < setup.devices.Count; i++)
            {
                var definition = setup.devices[i].definition;
                if (!rows.TryGetValue(definition, out var row))
                {
                    row = new DeviceRow(deviceRowTree, setup, definition, isOpen => SetOpenRow(isOpen ? definition : null));
                    rows.Add(definition, row);
                }

                // Moving an element detaches it, which drops its pointer capture; move only when out of place.
                if (list.IndexOf(row) != i)
                {
                    list.Insert(i, row);
                }

                row.SetOpen(definition.GetInstanceID() == openId);
                row.Refresh();
            }

            foreach (var definition in new List<DeviceDefinition>(rows.Keys))
            {
                if (setup.FindDevice(definition) == null)
                {
                    rows[definition].RemoveFromHierarchy();
                    rows.Remove(definition);
                }
            }

            var canAdd = GetMissingDefinitions().Count > 0;
            var addDevice = root.Q<Button>("add-device");
            addDevice.SetEnabled(canAdd);
            addDevice.tooltip = canAdd ? "" : "Every device is already on this avatar.";
        }

        /// Open the row for definition and close the others in place; null closes them all.
        private void SetOpenRow(DeviceDefinition definition)
        {
            SessionState.SetInt(OpenRowKey, definition != null ? definition.GetInstanceID() : 0);
            foreach (var row in rows.Values)
            {
                row.SetOpen(row.Definition == definition);
            }
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

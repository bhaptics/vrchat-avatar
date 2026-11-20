using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
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

            // Show the result at once rather than wait for an event that active and tag changes may not send.
            root.Q<Button>("switch-receivers").clicked += () =>
            {
                HapticsSetupActions.SwitchReceiverSets(setup);
                ShowPlatform();
            };

            // Setup data changes, Undo included, refresh the inspector; scene changes come through hierarchyChanged.
            root.TrackSerializedObjectValue(serializedObject, _ => Refresh());
            root.RegisterCallback<AttachToPanelEvent>(_ => Subscribe());
            root.RegisterCallback<DetachFromPanelEvent>(_ => Unsubscribe());

            Refresh();
            return root;
        }

        private void Subscribe()
        {
            EditorApplication.hierarchyChanged += Refresh;
        }

        private void Unsubscribe()
        {
            EditorApplication.hierarchyChanged -= Refresh;
        }

        /// Bring what the inspector shows up to date: the platform line and message, then the rows.
        private void Refresh()
        {
            ShowPlatform();
            SyncRows();
        }

        /// Show the build target's schema, and an error with a switch when a device uploads the other platform's set.
        private void ShowPlatform()
        {
            var isMobile = Platforms.IsBuildTargetMobile();
            var target = GetBuildTargetName();
            root.Q<InfoField>("build-target").text = isMobile ? $"{target} · Quest schema (v2m)" : $"{target} · PC schema (v2)";

            var mismatched = setup.devices.Exists(d => !Platforms.MatchesPlatform(d, isMobile));
            var message = root.Q<MessageBox>("platform-mismatch");
            message.style.display = mismatched ? DisplayStyle.Flex : DisplayStyle.None;
            message.text = $"Receivers are set up for {(isMobile ? "PC" : "Quest")}, but the build target is {target}, so the upload would carry the wrong schema.";
        }

        /// Returns the build target's name as Build Settings shows it, for the platforms VRChat builds.
        private static string GetBuildTargetName()
        {
            switch (EditorUserBuildSettings.activeBuildTarget)
            {
                case BuildTarget.Android:
                    return "Android";
                case BuildTarget.iOS:
                    return "iOS";
                case BuildTarget.StandaloneWindows:
                case BuildTarget.StandaloneWindows64:
                    return "Windows";
                default:
                    return EditorUserBuildSettings.activeBuildTarget.ToString();
            }
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

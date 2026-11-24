using System.Collections.Generic;
using System.Linq;
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
        [SerializeField] private DeviceCatalog catalog;

        private readonly Dictionary<DeviceDefinition, DeviceRow> rows = new();

        private HapticsSetup setup;
        private VisualElement root;

        private DeviceDefinition[] Definitions => catalog.definitions;

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

        /// Bring what the inspector shows up to date: the setup's messages, the platform line and message, the rows, then Add Device.
        private void Refresh()
        {
            ShowSetupMessages();
            ShowPlatform();
            ShowUploadProblems();
            SyncRows();
            ShowAddDevice();
        }

        /// Show what keeps this setup from working as usual: a prefab asset, a setup off the avatar root, a rig that isn't Humanoid.
        private void ShowSetupMessages()
        {
            var isAsset = EditorUtility.IsPersistent(setup);
            var onRoot = Placement.IsAvatarRoot(setup.transform);
            Show("prefab-asset", isAsset);
            Show("not-on-root", !isAsset && !onRoot);
            Show("not-humanoid", onRoot && !Placement.IsHumanoid(setup.transform));
            // The actions refuse a prefab asset, so nothing in the body could act on it.
            root.Q("body").SetEnabled(!isAsset);
        }

        /// Show the build target's schema, and an error with a switch when a device uploads the other platform's set.
        private void ShowPlatform()
        {
            var isMobile = Platforms.IsBuildTargetMobile();
            var target = GetBuildTargetName();
            root.Q<InfoField>("build-target").text = isMobile ? $"{target} · Quest schema (v2m)" : $"{target} · PC schema (v2)";

            Show("platform-mismatch", Platforms.HasMismatch(setup, isMobile));
            root.Q<MessageBox>("platform-mismatch").text =
                $"Receivers are set up for {(isMobile ? "PC" : "Quest")}, but the build target is {target}, so the upload would carry the wrong schema. Uploads are blocked until you switch them.";
        }

        /// Show what blocks the upload, one line each, as the build callback would report it; a set mismatch is left to its own message and its switch.
        private void ShowUploadProblems()
        {
            var isMobile = Platforms.IsBuildTargetMobile();
            var mismatch = Platforms.HasMismatch(setup, isMobile);
            var lines = UploadChecks.GetProblems(setup.transform, isMobile)
                .Where(problem => !(mismatch && problem.Kind == UploadChecks.ProblemKind.WrongSchema))
                .Select(problem => problem.Message)
                .ToList();
            Show("upload-blocked", lines.Count > 0);
            root.Q<MessageBox>("upload-blocked").text = "Uploads are blocked: " + string.Join("\n", lines);
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
        }

        /// Enable Add Device while a device can be added; disabled, its tooltip says why.
        private void ShowAddDevice()
        {
            var blocker = FindAddDeviceBlocker();
            var addDevice = root.Q<Button>("add-device");
            addDevice.SetEnabled(blocker == null);
            addDevice.tooltip = blocker ?? "";
        }

        /// Returns why no device can be added now, or null when one can.
        private string FindAddDeviceBlocker()
        {
            if (!Placement.IsAvatarRoot(setup.transform))
            {
                return "Put bHaptics Setup on the avatar root to add devices.";
            }

            if (GetMissingDefinitions().Count == 0)
            {
                return "Every device is already on this avatar.";
            }

            return null;
        }

        /// Show the element called name, or hide it.
        private void Show(string name, bool visible)
        {
            root.Q(name).style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
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

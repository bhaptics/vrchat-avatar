using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace bHaptics.VRChatAvatar
{
    [CustomEditor(typeof(HapticsSetup))]
    public class HapticsSetupEditor : Editor
    {
        private const string AacListing = "https://hai-vr.github.io/vpm-listing/index.json";

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

            FillIndicators();

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
            // An undone Apply changes only the descriptor and assets, which hierarchyChanged doesn't report.
            Undo.undoRedoPerformed += Refresh;
        }

        private void Unsubscribe()
        {
            EditorApplication.hierarchyChanged -= Refresh;
            Undo.undoRedoPerformed -= Refresh;
        }

        /// Bring what the inspector shows up to date: the setup's messages, the platform line and message, the rows, then Add Device.
        private void Refresh()
        {
            ShowSetupMessages();
            ShowPlatform();
            ShowUploadProblems();
            SyncRows();
            ShowAddDevice();
            ShowIndicators();
        }

        /// Wire the Indicator Apply section: the read-only slot fields, the AAC listing buttons and Apply.
        private void FillIndicators()
        {
            // Read-only: they show the descriptor's slots; a drop or a pick goes back through Refresh.
            var fx = root.Q<ObjectField>("fx");
            fx.objectType = typeof(AnimatorController);
            fx.RegisterValueChangedCallback(_ => Refresh());
            var parameters = root.Q<ObjectField>("parameters");
            parameters.objectType = typeof(VRCExpressionParameters);
            parameters.RegisterValueChangedCallback(_ => Refresh());

            root.Q<Button>("aac-vcc").clicked += () => Application.OpenURL($"vcc://vpm/addRepo?url={AacListing}");
            root.Q<Button>("aac-copy").clicked += () => EditorGUIUtility.systemCopyBuffer = AacListing;
            root.Q<Button>("apply").clicked += () =>
            {
                IndicatorApplier.Apply(setup);
                Refresh();
            };

            // The slots live on the descriptor; root already tracks the setup, and an element tracks one object at a time.
            var descriptor = setup.GetComponent<VRCAvatarDescriptor>();
            if (descriptor != null)
            {
                root.Q("indicators").TrackSerializedObjectValue(new SerializedObject(descriptor), _ => Refresh());
            }
        }

        /// Show what Apply Indicators would change for the build target, what blocks it, and the synced bits it leaves.
        private void ShowIndicators()
        {
            var onAvatar = setup.GetComponent<VRCAvatarDescriptor>() != null;
            Show("indicators", onAvatar);
            Show("synced-parameters", onAvatar);
            if (!onAvatar)
            {
                return;
            }

            var isMobile = Platforms.IsBuildTargetMobile();
            var plan = IndicatorPlanner.Plan(setup, isMobile);
            var aacMissing = plan.layers.Count > 0 && IndicatorApplier.LayerWriter == null;

            var bar = root.Q<ProgressBar>("bits-bar");
            bar.value = plan.bitsAfter;
            bar.title = $"{plan.bitsAfter} / {VRCExpressionParameters.MAX_PARAMETER_COST} bits, bHaptics {plan.bools.Count}";

            Show("quest-indicators", isMobile && plan.bools.Count > 0);
            Show("aac-missing", aacMissing);
            root.Q<ObjectField>("fx").SetValueWithoutNotify(plan.fx);
            root.Q<ObjectField>("parameters").SetValueWithoutNotify(plan.parameters);

            Show("apply-blocked", plan.blockers.Count > 0);
            root.Q<MessageBox>("apply-blocked").text = string.Join("\n", plan.blockers);
            Show("apply-will", !plan.IsUpToDate);
            FillApplyLines(plan);

            root.Q<Label>("apply-status").text = plan.IsUpToDate ? "Up to date" : "Not applied";
            root.Q<Button>("apply").SetEnabled(!plan.IsUpToDate && plan.blockers.Count == 0 && !aacMissing);
        }

        /// List what Apply would copy, change and delete; what it deletes or rewrites, edits included, is in amber.
        private void FillApplyLines(IndicatorPlan plan)
        {
            var lines = root.Q("apply-lines");
            lines.Clear();
            var now = DateTime.Now;
            if (plan.ChangesFx)
            {
                AddLine(lines, "FX", "bh-line--title");
                AddLine(lines, plan.fx != null
                    ? $"Copy {plan.fx.name} to {GeneratedAssets.GetPath(plan.fx.name, "controller", now)}"
                    : $"Create {GeneratedAssets.GetPath($"{setup.name}_FX", "controller", now)}");
                AddCount(lines, plan.layersToAdd.Count, "Add {0} indicator layers");
                AddCount(lines, plan.layersToReplace.Count, "Rewrite {0} indicator layers, dropping any edits to them", "bh-line--warn");
                AddCount(lines, plan.layersToRemove.Count, "Delete {0} indicator layers", "bh-line--warn");
                AddCount(lines, plan.fxParametersToRemove.Count, "Delete {0} bHaptics parameters no layer reads", "bh-line--warn");
                AddLine(lines, plan.fx != null ? "Point the FX slot at the copy" : "Point the FX slot at the new controller");
            }

            if (plan.ChangesParameters)
            {
                AddLine(lines, "Expression Parameters", "bh-line--title");
                AddLine(lines, plan.parameters != null
                    ? $"Copy {plan.parameters.name} to {GeneratedAssets.GetPath(plan.parameters.name, "asset", now)}"
                    : $"Copy VRChat's default parameters and menu to {GeneratedAssets.GetPath($"{setup.name}_Parameters", "asset", now)}");
                AddCount(lines, plan.boolsToAdd.Count, "Add {0} synced Bools at the end");
                AddCount(lines, plan.boolsToRemove.Count, "Delete {0} synced Bools", "bh-line--warn");
                AddLine(lines, "Point the Expression Parameters slot at the copy");
            }
        }

        private static void AddCount(VisualElement lines, int count, string format, string className = null)
        {
            if (count > 0)
            {
                AddLine(lines, string.Format(format, count), className);
            }
        }

        private static void AddLine(VisualElement lines, string text, string className = null)
        {
            var line = new Label(text);
            line.AddToClassList("bh-line");
            if (className != null)
            {
                line.AddToClassList(className);
            }

            lines.Add(line);
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

        /// Match the rows to setup's devices in catalogue order: keep existing rows, add missing ones, drop removed ones, then refresh them.
        private void SyncRows()
        {
            var list = root.Q("device-list");
            var openId = SessionState.GetInt(OpenRowKey, 0);
            // A fixed order, not the order they were added; devices the catalogue doesn't list go last.
            var devices = setup.devices.OrderBy(d => GetCatalogIndex(d.definition)).ToList();
            for (var i = 0; i < devices.Count; i++)
            {
                var definition = devices[i].definition;
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
                var label = new GUIContent($"{definition.displayName} ({definition.productName})");
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

        /// Returns definition's position in the catalogue, or int.MaxValue when the catalogue doesn't list it.
        private int GetCatalogIndex(DeviceDefinition definition)
        {
            var index = System.Array.IndexOf(Definitions, definition);
            return index >= 0 ? index : int.MaxValue;
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

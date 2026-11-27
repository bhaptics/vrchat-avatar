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
    internal class HapticsSetupEditor : Editor
    {
        // The bHaptics listing also carries Animator As Code.
        private const string Listing = "https://vpm.bhaptics.com/index.json";

        // A menu item can't reach an editor instance, so the ⋮ item raises this and the open inspector answers.
        private static event Action<HapticsSetup> RemoveRequested;

        // Default references, set on this script's import settings.
        [SerializeField] private VisualTreeAsset inspectorTree;
        [SerializeField] private VisualTreeAsset deviceRowTree;
        [SerializeField] private DeviceCatalog catalog;

        private readonly Dictionary<DeviceDefinition, DeviceRow> rows = new();

        private HapticsSetup setup;
        private VisualElement root;
        private bool removeOpen;

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
            FillRemove();
            FillLegacy();

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
            RemoveRequested += OnRemoveRequested;
        }

        private void Unsubscribe()
        {
            EditorApplication.hierarchyChanged -= Refresh;
            Undo.undoRedoPerformed -= Refresh;
            RemoveRequested -= OnRemoveRequested;
        }

        [MenuItem("CONTEXT/HapticsSetup/Remove bHaptics from Avatar…")]
        private static void RequestRemove(MenuCommand command)
        {
            RemoveRequested?.Invoke((HapticsSetup)command.context);
        }

        private void Refresh()
        {
            // Remove bHaptics destroys the setup, and the hierarchy event can come before this inspector goes.
            if (setup == null)
            {
                return;
            }

            // One plan for the build target serves the legacy warning and the Indicator Apply section.
            var plan = IndicatorPlanner.Plan(setup, Platforms.IsBuildTargetMobile());
            ShowSetupMessages();
            ShowPlatform();
            ShowUploadProblems();
            ShowLegacy(plan);
            SyncRows();
            ShowAddDevice();
            ShowIndicators(plan);
            if (removeOpen)
            {
                FillRemoveLines();
            }
        }

        /// The button opens the deletion list below it, the ⋮ item at the top; Remove carries it out.
        private void FillRemove()
        {
            Show("remove-confirm", false);
            root.Q<Button>("remove-setup").clicked += () => OpenRemove("remove-bottom");
            root.Q<Button>("remove-cancel").clicked += () =>
            {
                removeOpen = false;
                Show("remove-confirm", false);
            };
            root.Q<Button>("remove-confirmed").clicked += () => HapticsSetupActions.RemoveSetup(setup);
        }

        private void OnRemoveRequested(HapticsSetup requested)
        {
            if (requested == setup)
            {
                OpenRemove("remove-top");
            }
        }

        /// Show the deletion list in the container called place, where the request came from.
        private void OpenRemove(string place)
        {
            root.Q(place).Add(root.Q("remove-confirm"));
            removeOpen = true;
            FillRemoveLines();
            Show("remove-confirm", true);
        }

        /// List what Remove deletes: the device objects and the
        /// component, then the slot changes the way Apply lists them.
        private void FillRemoveLines()
        {
            var lines = root.Q("remove-lines");
            lines.Clear();
            var devices = setup.devices.Where(d => d.definition != null && d.deviceRoot != null && setup.IsOnThisAvatar(d)).Select(d => d.definition.displayName).ToList();
            if (devices.Count > 0)
            {
                AddLine(lines, $"Delete the device objects: {string.Join(", ", devices)}");
            }

            AddLine(lines, "Remove bHaptics Setup");
            var plan = IndicatorPlanner.PlanRemoval(setup);
            // Without a descriptor there are no slots to change.
            if (plan.blockers.Count == 0)
            {
                AddPlanLines(lines, plan);
            }
        }

        /// Wire the legacy warning's button: it selects the receivers to delete, and deletes nothing itself.
        private void FillLegacy()
        {
            root.Q<Button>("legacy-select").clicked += () =>
                Selection.objects = LegacyLeftovers.FindForeignReceivers(setup).Select(r => (UnityEngine.Object)r.gameObject).Distinct().ToArray();
        }

        /// Show what the legacy package left, a line per kind with its own way out; the box hides once nothing is left.
        private void ShowLegacy(IndicatorPlan plan)
        {
            var receivers = LegacyLeftovers.FindForeignReceivers(setup).Count;
            var layers = plan.legacyLayersToRemove.Count;
            var folder = LegacyLeftovers.FindFolder();
            Show("legacy", receivers > 0 || layers > 0 || folder != null);
            Show("legacy-receivers", receivers > 0);
            root.Q<Label>("legacy-receivers").text = $"• {receivers} receivers with bHaptics addresses aren't part of a device here. Delete the ones the legacy package left.";
            Show("legacy-fx", layers > 0);
            root.Q<Label>("legacy-fx").text = $"• {layers} layers in the FX. Apply Indicators removes them.";
            Show("legacy-folder", folder != null);
            root.Q<Label>("legacy-folder").text = $"• {folder} is still in the project. Delete it.";
            Show("legacy-buttons", receivers > 0);
        }

        private void FillIndicators()
        {
            // Read-only: they show the descriptor's slots; a cleared field (Delete key) goes back through Refresh.
            var fx = root.Q<ObjectField>("fx");
            fx.objectType = typeof(AnimatorController);
            ReadOnlyObjectField.Apply(fx);
            fx.RegisterValueChangedCallback(_ => Refresh());
            var parameters = root.Q<ObjectField>("parameters");
            parameters.objectType = typeof(VRCExpressionParameters);
            ReadOnlyObjectField.Apply(parameters);
            parameters.RegisterValueChangedCallback(_ => Refresh());

            root.Q<Button>("aac-vcc").clicked += () => Application.OpenURL($"vcc://vpm/addRepo?url={Listing}");
            root.Q<Button>("aac-copy").clicked += () => EditorGUIUtility.systemCopyBuffer = Listing;
            root.Q<Button>("apply").clicked += () =>
            {
                IndicatorApplier.Apply(setup);
                Refresh();
            };

            // The slots live on the descriptor; root already tracks
            // the setup, and an element tracks one object at a time.
            var descriptor = setup.GetComponent<VRCAvatarDescriptor>();
            if (descriptor != null)
            {
                root.Q("indicators").TrackSerializedObjectValue(new SerializedObject(descriptor), _ => Refresh());
            }
        }

        /// Show what Apply Indicators would change for the build target, what blocks it, and the synced bits it leaves.
        private void ShowIndicators(IndicatorPlan plan)
        {
            var onAvatar = setup.GetComponent<VRCAvatarDescriptor>() != null;
            Show("indicators", onAvatar);
            Show("synced-parameters", onAvatar);
            if (!onAvatar)
            {
                return;
            }

            var isMobile = Platforms.IsBuildTargetMobile();
            var aacMissing = plan.layers.Count > 0 && IndicatorApplier.LayerWriter == null;

            var bar = root.Q<ProgressBar>("bits-bar");
            bar.value = plan.bitsAfter;
            bar.title = $"{plan.bitsAfter} / {VRCExpressionParameters.MAX_PARAMETER_COST} bits, bHaptics {plan.bools.Count}";

            Show("indicators-lit", plan.LeavesIndicatorsLit);
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

        private void FillApplyLines(IndicatorPlan plan)
        {
            var lines = root.Q("apply-lines");
            lines.Clear();
            AddPlanLines(lines, plan);
        }

        /// List plan's slot changes. Lines that delete or rewrite the user's content are amber.
        private void AddPlanLines(VisualElement lines, IndicatorPlan plan)
        {
            var now = DateTime.Now;
            if (plan.ChangesFx)
            {
                AddLine(lines, "FX", "bh-line--title");
                AddLine(lines, plan.fx != null
                    ? $"Use a copy named {GetCopyName(plan.fx.name, "controller", now)}"
                    : $"Use a new controller named {GetCopyName($"{setup.name}_FX", "controller", now)}");
                AddCount(lines, plan.layersToAdd.Count, "Add {0} indicator layers");
                AddCount(lines, plan.LayersRewritten, "Rewrite {0} indicator layers, losing edits to them", "bh-line--warn");
                AddNamed(lines, plan.layersToRemove, "Delete {0} indicator layers", "bh-line--warn");
                AddCount(lines, plan.legacyLayersToRemove.Count, "Delete {0} legacy bHaptics layers", "bh-line--warn");
                AddCount(lines, plan.fxParametersToRetype.Count, "Fix the type of {0} bHaptics parameters");
                AddNamed(lines, plan.fxParametersToRemove, "Delete {0} bHaptics parameters", "bh-line--warn");
            }

            if (plan.ChangesParameters)
            {
                AddLine(lines, "Expression Parameters", "bh-line--title");
                AddLine(lines, plan.parameters != null
                    ? $"Use a copy named {GetCopyName(plan.parameters.name, "asset", now)}"
                    : $"Use a copy of VRChat's defaults named {GetCopyName($"{setup.name}_Parameters", "asset", now)}");
                if (plan.copiesDefaultMenu)
                {
                    AddLine(lines, $"Add an empty Expressions Menu named {GetCopyName($"{setup.name}_Menu", "asset", now)}, which replaces VRChat's default emotes", "bh-line--warn");
                }

                AddCount(lines, plan.boolsToAdd.Count, "Add {0} synced Bools at the end");
                AddCount(lines, plan.boolsToFix.Count, "Fix {0} synced Bools");
                AddNamed(lines, plan.boolsToRemove, "Delete {0} synced Bools", "bh-line--warn");
            }

            if (plan.ChangesFx || plan.ChangesParameters)
            {
                AddLine(lines, $"Copies go to {GeneratedAssets.Folder}.");
            }
        }

        /// Returns the file name, without extension, a copy of name would get now.
        private static string GetCopyName(string name, string extension, DateTime now)
        {
            return System.IO.Path.GetFileNameWithoutExtension(GeneratedAssets.GetPath(name, extension, now));
        }

        /// Add a count line that names what it counts, up to three, so a deletion of the user's own content shows.
        private static void AddNamed(VisualElement lines, List<string> names, string format, string className)
        {
            if (names.Count == 0)
            {
                return;
            }

            var shown = string.Join(", ", names.Take(3));
            if (names.Count > 3)
            {
                shown += $" and {names.Count - 3} more";
            }

            AddLine(lines, $"{string.Format(format, names.Count)}: {shown}", className);
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

        /// Show what keeps this setup from working as usual: a prefab
        /// asset, a setup off the avatar root, a rig that isn't Humanoid.
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

        /// Show what blocks the upload, one line each, as the build callback would report it; a set mismatch is left to
        /// its own message and its switch.
        private void ShowUploadProblems()
        {
            var isMobile = Platforms.IsBuildTargetMobile();
            var mismatch = Platforms.HasMismatch(setup, isMobile);
            var lines = UploadChecks.GetProblems(setup.transform, isMobile)
                .Where(problem => !(mismatch && problem.kind == UploadChecks.ProblemKind.WrongSchema))
                .Select(problem => problem.message)
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

        /// Rows are kept across refreshes, so a field in use survives.
        private void SyncRows()
        {
            var list = root.Q("device-list");
            var openId = SessionState.GetInt(OpenRowKey, 0);
            // Catalog order, not the order they were added; devices the catalog doesn't list go last.
            // An entry whose definition asset is gone gets no row: a row is keyed and titled by its definition.
            var devices = setup.devices.Where(d => d.definition != null).OrderBy(d => GetCatalogIndex(d.definition)).ToList();
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

        private int GetCatalogIndex(DeviceDefinition definition)
        {
            var index = System.Array.IndexOf(Definitions, definition);
            return index >= 0 ? index : int.MaxValue;
        }

        /// Get the definitions not on this avatar yet.
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

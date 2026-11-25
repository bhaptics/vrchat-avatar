using System;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace bHaptics.VRChatAvatar
{
    /// One device's row in the HapticsSetup inspector. Built once and kept: Refresh updates what it shows, so a field in use survives.
    internal class DeviceRow : VisualElement
    {
        private readonly HapticsSetup setup;
        private Transform boundOffset;

        public DeviceDefinition Definition { get; }

        /// onToggle gets the foldout's new value; the inspector decides which row is open.
        public DeviceRow(VisualTreeAsset tree, HapticsSetup setup, DeviceDefinition definition, Action<bool> onToggle)
        {
            tree.CloneTree(this);
            this.setup = setup;
            Definition = definition;

            FillHeader(onToggle);
            FillDeviceRoot();
            FillObjectsMissing();
            FillRotation();
            FillScale();
            FillScaleLock();
            FillShowMesh();
            FillShowIndicator();
            FillReset();
        }

        /// Show or hide the content without notifying onToggle.
        public void SetOpen(bool open)
        {
            this.Q<Foldout>("title").SetValueWithoutNotify(open);
            Show("content", open);
        }

        /// Update what the row shows from the device's current objects; the elements stay.
        public void Refresh()
        {
            var device = setup.FindDevice(Definition);
            if (device == null)
            {
                return;
            }

            var deviceRoot = this.Q<ObjectField>("device-root");
            // The field redraws only when the value differs, and a deleted root still equals its own reference; going through null forces it.
            deviceRoot.SetValueWithoutNotify(null);
            deviceRoot.SetValueWithoutNotify(device.deviceRoot);
            deviceRoot.EnableInClassList("bh-missing", device.deviceRoot == null);

            // The Offset goes with a deleted device root; either way nothing is left to vibrate or to reset. An entry pasted from another avatar can't act on that avatar's objects.
            var elsewhere = !setup.IsOnThisAvatar(device);
            var broken = device.offset == null || elsewhere;
            Show("warn", broken);
            Show("objects-missing", broken);
            this.Q<MessageBox>("objects-missing").text = elsewhere
                ? $"{Definition.displayName}'s device objects are on another avatar, so it won't vibrate here. Remove it, then add it again with Add Device; the other avatar keeps its objects."
                : $"{Definition.displayName}'s device objects are missing, so it won't vibrate. Remove it, then add it again with Add Device.";
            Show("fingers-missing", !broken && DevicePlacements.IsMissingFingers(device));
            Show("reset-line", !broken);
            RefreshOffset(device.offset);

            this.Q<Toggle>("show-mesh").SetValueWithoutNotify(device.showMesh);
            // A definition without meshes has nothing to show.
            Show("show-mesh", !broken && (Definition.pcMesh != null || Definition.questMesh != null));

            this.Q<Toggle>("show-indicator").SetValueWithoutNotify(device.showIndicator);
            Show("show-indicator", !broken && Definition.pcIndicatorModel != null);
        }

        /// Fill the title foldout, the product name and the remove button, and let a click anywhere else on the line toggle the row.
        private void FillHeader(Action<bool> onToggle)
        {
            var title = this.Q<Foldout>("title");
            title.text = Definition.displayName;
            title.RegisterValueChangedCallback(e => onToggle(e.newValue));

            var subtitle = this.Q<Label>("subtitle");
            subtitle.text = Definition.productName;

            var warn = this.Q("warn");
            warn.style.backgroundImage = (Texture2D)EditorGUIUtility.IconContent("console.warnicon.sml").image;

            // The rest of the header line toggles too; the foldout handles its own clicks, the remove button its own.
            var header = this.Q("header");
            header.RegisterCallback<ClickEvent>(e =>
            {
                if (e.target == header || e.target == subtitle || e.target == warn)
                {
                    title.value = !title.value;
                }
            });

            var remove = this.Q<Button>("remove");
            remove.tooltip = $"Remove {Definition.displayName}";
            // Without the d_ prefix, IconContent picks the icon for the current editor theme.
            remove.style.backgroundImage = (Texture2D)EditorGUIUtility.IconContent("winbtn_win_close").image;
            remove.clicked += () => HapticsSetupActions.RemoveDevice(setup, Definition);
        }

        /// Keep the device root field read-only: it's there to ping; a cleared field (Delete key) goes back through Refresh.
        private void FillDeviceRoot()
        {
            var deviceRoot = this.Q<ObjectField>("device-root");
            ReadOnlyObjectField.Apply(deviceRoot);
            deviceRoot.RegisterValueChangedCallback(_ => Refresh());
        }

        /// Wire the button in the message shown when the device's objects are gone or elsewhere: there's no Recreate, so it's removed and added again.
        private void FillObjectsMissing()
        {
            this.Q<Button>("remove-device").clicked += () => HapticsSetupActions.RemoveDevice(setup, Definition);
        }

        /// Write Rotation through TransformUtils, as the Transform inspector does, so the angles typed stay (180 stays 180, 370 stays 370).
        private void FillRotation()
        {
            this.Q<Vector3Field>("rotation").RegisterValueChangedCallback(e =>
            {
                Undo.RecordObject(boundOffset, "Modified Local Rotation in Offset");
                TransformUtils.SetInspectorRotation(boundOffset, e.newValue);
                PrefabUtility.RecordPrefabInstancePropertyModifications(boundOffset);
            });
        }

        /// Write Scale by hand, not by binding, so that with the lock on the other axes follow the edited one.
        private void FillScale()
        {
            var scale = this.Q<Vector3Field>("scale");
            scale.RegisterValueChangedCallback(e =>
            {
                var value = e.newValue;
                if (TransformUtils.GetConstrainProportions(boundOffset))
                {
                    value = ConstrainScale(e.previousValue, e.newValue);
                }

                Undo.RecordObject(boundOffset, "Modified Local Scale in Offset");
                boundOffset.localScale = value;
                PrefabUtility.RecordPrefabInstancePropertyModifications(boundOffset);
                scale.SetValueWithoutNotify(value);
            });
        }

        /// Put the lock at the end of the Scale label, where the Transform inspector has its link; it toggles the Offset's own flag.
        private void FillScaleLock()
        {
            var scaleLock = this.Q<Button>("scale-lock");
            // UXML can't reach inside a field's label, so the lock moves there from where it's declared.
            this.Q<Vector3Field>("scale").labelElement.Add(scaleLock);
            // SetConstrainProportions records its own Undo (measured).
            scaleLock.clicked += () =>
            {
                TransformUtils.SetConstrainProportions(boundOffset, !TransformUtils.GetConstrainProportions(boundOffset));
                PrefabUtility.RecordPrefabInstancePropertyModifications(boundOffset);
                ShowOffsetValues();
            };
        }

        /// Show or hide the device's meshes from the toggle; SetMeshShown records the setting and the meshes for Undo.
        private void FillShowMesh()
        {
            this.Q<Toggle>("show-mesh").RegisterValueChangedCallback(e => HapticsSetupActions.SetMeshShown(setup, Definition, e.newValue));
        }

        /// Show or hide the device's indicator from the toggle; its text, right of the box, counts the synced bits it takes, one per PC node.
        private void FillShowIndicator()
        {
            var toggle = this.Q<Toggle>("show-indicator");
            toggle.text = $"{Definition.pc.Length} synced bits";
            toggle.RegisterValueChangedCallback(e => HapticsSetupActions.SetIndicatorShown(setup, Definition, e.newValue));
        }

        /// Rebuild the device's receiver sets, meshes and indicator on click, with no confirmation: Undo reverts it.
        private void FillReset()
        {
            this.Q<Button>("reset").clicked += () => HapticsSetupActions.ResetDevice(setup, Definition);
        }

        /// Show offset's Position, Rotation, Scale and scale lock, hidden when it's gone. Rebinds only when the Offset object changes: rebinding a field in use would drop the drag.
        private void RefreshOffset(Transform offset)
        {
            // Forget a deleted Offset, so the same object brought back by Undo binds again.
            if (offset == null)
            {
                boundOffset = null;
                Show("offset", false);
                return;
            }

            if (offset == boundOffset)
            {
                return;
            }

            boundOffset = offset;
            Show("offset", true);
            var serialized = new SerializedObject(offset);
            this.Q<Vector3Field>("position").BindProperty(serialized.FindProperty("m_LocalPosition"));

            ShowOffsetValues();
            // Rotation, Scale and the lock aren't bound, so follow the Offset's changes made elsewhere, Undo included.
            // An element tracks one SerializedObject at a time and throws on a second, so drop the previous Offset's first; Unbind clears its children's bindings too, so bind nothing inside offset-manual.
            var manual = this.Q("offset-manual");
            manual.Unbind();
            manual.TrackSerializedObjectValue(serialized, _ => ShowOffsetValues());
        }

        /// Show the Offset's Rotation, Scale and scale lock, the parts that aren't bound.
        private void ShowOffsetValues()
        {
            this.Q<Vector3Field>("rotation").SetValueWithoutNotify(TransformUtils.GetInspectorRotation(boundOffset));
            this.Q<Vector3Field>("scale").SetValueWithoutNotify(boundOffset.localScale);

            var locked = TransformUtils.GetConstrainProportions(boundOffset);
            var scaleLock = this.Q<Button>("scale-lock");
            // Without the d_ prefix, IconContent picks the icon for the current editor theme.
            scaleLock.style.backgroundImage = (Texture2D)EditorGUIUtility.IconContent(locked ? "Linked" : "Unlinked").image;
            scaleLock.tooltip = locked ? "Disable constrained proportions" : "Enable constrained proportions";
        }

        /// Scale the other axes by the first edited axis's ratio, as the Transform inspector does.
        /// An axis at zero gives no ratio, so then only that axis changes.
        private static Vector3 ConstrainScale(Vector3 previous, Vector3 value)
        {
            for (var axis = 0; axis < 3; axis++)
            {
                if (Mathf.Approximately(value[axis], previous[axis]))
                {
                    continue;
                }

                if (Mathf.Approximately(previous[axis], 0f))
                {
                    return value;
                }

                return previous * (value[axis] / previous[axis]);
            }

            return value;
        }

        /// Show the element called name, or hide it.
        private void Show(string name, bool visible)
        {
            this.Q(name).style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}

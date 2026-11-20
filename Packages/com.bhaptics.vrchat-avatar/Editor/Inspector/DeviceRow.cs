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
            FillReset();
        }

        /// Show or hide the content without notifying onToggle.
        public void SetOpen(bool open)
        {
            this.Q<Foldout>("title").SetValueWithoutNotify(open);
            this.Q("content").style.display = open ? DisplayStyle.Flex : DisplayStyle.None;
        }

        /// Update what the row shows from the device's current objects; the elements stay.
        public void Refresh()
        {
            var device = setup.FindDevice(Definition);
            if (device == null)
            {
                return;
            }

            this.Q<ObjectField>("device-root").SetValueWithoutNotify(device.deviceRoot);
            RefreshOffset(device.offset);
        }

        /// Fill the title foldout, the product name and the remove button, and let a click anywhere else on the line toggle the row.
        private void FillHeader(Action<bool> onToggle)
        {
            var title = this.Q<Foldout>("title");
            title.text = Definition.displayName;
            title.RegisterValueChangedCallback(e => onToggle(e.newValue));

            var subtitle = this.Q<Label>("subtitle");
            subtitle.text = Definition.productName;

            // The rest of the header line toggles too; the foldout handles its own clicks, the remove button its own.
            var header = this.Q("header");
            header.RegisterCallback<ClickEvent>(e =>
            {
                if (e.target == header || e.target == subtitle)
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

        /// Keep the device root field read-only: it's there to ping; a drop or a pick goes back through Refresh.
        private void FillDeviceRoot()
        {
            this.Q<ObjectField>("device-root").RegisterValueChangedCallback(_ => Refresh());
        }

        /// Rebuild the device's receiver sets and meshes on click, with no confirmation: Undo reverts it.
        private void FillReset()
        {
            this.Q<Button>("reset").clicked += () => HapticsSetupActions.ResetDevice(setup, Definition);
        }

        /// Bind Position and Scale to offset, hidden when it's gone. Rebinds only when the Offset object changes: rebinding a field in use would drop the drag.
        private void RefreshOffset(Transform offset)
        {
            // Forget a deleted Offset, so the same object brought back by Undo binds again.
            if (offset == null)
            {
                boundOffset = null;
                this.Q("offset").style.display = DisplayStyle.None;
                return;
            }

            if (offset == boundOffset)
            {
                return;
            }

            boundOffset = offset;
            this.Q("offset").style.display = DisplayStyle.Flex;
            var serialized = new SerializedObject(offset);
            this.Q<Vector3Field>("position").BindProperty(serialized.FindProperty("m_LocalPosition"));
            this.Q<Vector3Field>("scale").BindProperty(serialized.FindProperty("m_LocalScale"));
        }
    }
}

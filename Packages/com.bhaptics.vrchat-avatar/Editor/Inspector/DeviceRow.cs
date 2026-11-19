using System;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace bHaptics.VRChatAvatar
{
    /// One device's row in the HapticsSetup inspector: the header, and the content while the row is open.
    internal class DeviceRow : VisualElement
    {
        private readonly HapticsSetup setup;
        private readonly HapticDevice device;

        /// onToggle gets the foldout's new value; the inspector decides which row is open.
        public DeviceRow(VisualTreeAsset tree, HapticsSetup setup, HapticDevice device, bool open, Action<bool> onToggle)
        {
            tree.CloneTree(this);
            this.setup = setup;
            this.device = device;

            FillHeader(open, onToggle);

            this.Q("content").style.display = open ? DisplayStyle.Flex : DisplayStyle.None;
            if (!open)
            {
                return;
            }

            FillDeviceRoot();
        }

        /// Fill the title foldout, the product name and the remove button.
        private void FillHeader(bool open, Action<bool> onToggle)
        {
            var title = this.Q<Foldout>("title");
            title.text = device.definition.displayName;
            title.SetValueWithoutNotify(open);
            title.RegisterValueChangedCallback(e => onToggle(e.newValue));

            this.Q<Label>("subtitle").text = device.definition.productName;

            var remove = this.Q<Button>("remove");
            remove.tooltip = $"Remove {device.definition.displayName}";
            remove.style.backgroundImage = (Texture2D)EditorGUIUtility.IconContent("d_winbtn_win_close").image;
            remove.clicked += () => HapticsSetupActions.RemoveDevice(setup, device.definition);
        }

        /// Show the device root read-only: the field is there to ping; a drop or a pick goes back.
        private void FillDeviceRoot()
        {
            var deviceRoot = this.Q<ObjectField>("device-root");
            deviceRoot.SetValueWithoutNotify(device.deviceRoot);
            deviceRoot.RegisterValueChangedCallback(_ => deviceRoot.SetValueWithoutNotify(device.deviceRoot));
        }
    }
}

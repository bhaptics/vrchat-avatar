using UnityEngine.UIElements;

namespace bHaptics.VRChatAvatar
{
    /// A HelpBox whose children go under its text, so buttons and lists keep the icon on their left.
    internal class MessageBox : HelpBox
    {
        public new class UxmlFactory : UxmlFactory<MessageBox, UxmlTraits>
        {
        }

        public new class UxmlTraits : HelpBox.UxmlTraits
        {
            public override void Init(VisualElement ve, IUxmlAttributes bag, CreationContext cc)
            {
                base.Init(ve, bag, cc);
                ((MessageBox)ve).PlaceIcon();
            }
        }

        private readonly VisualElement extra;

        // HelpBox builds its icon and text before extra exists, so those still land in the box itself.
        public override VisualElement contentContainer => extra ?? this;

        public MessageBox() : this(string.Empty, HelpBoxMessageType.None)
        {
        }

        public MessageBox(string text, HelpBoxMessageType type) : base(text, type)
        {
            AddToClassList("bh-message");
            var label = this.Q<Label>(className: labelUssClassName);
            var column = new VisualElement();
            column.AddToClassList("bh-message__column");
            hierarchy.Insert(hierarchy.IndexOf(label), column);
            column.Add(label);
            extra = new VisualElement();
            extra.AddToClassList("bh-message__extra");
            column.Add(extra);
        }

        /// Move the icon back in front of the text: setting messageType re-inserts it through contentContainer, under the text.
        private void PlaceIcon()
        {
            var icon = this.Q(className: iconUssClassName);
            if (icon == null || icon.hierarchy.parent == this)
            {
                return;
            }

            hierarchy.Insert(0, icon);
        }
    }
}

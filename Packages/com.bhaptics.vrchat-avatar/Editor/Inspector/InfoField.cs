using UnityEngine.UIElements;

namespace bHaptics.VRChatAvatar
{
    /// A label plus read-only text or child elements, aligned with the inspector's own fields.
    internal class InfoField : BaseField<string>
    {
        public new class UxmlFactory : UxmlFactory<InfoField, UxmlTraits>
        {
        }

        public new class UxmlTraits : BaseField<string>.UxmlTraits
        {
            private readonly UxmlStringAttributeDescription text = new UxmlStringAttributeDescription { name = "text" };

            public override void Init(VisualElement ve, IUxmlAttributes bag, CreationContext cc)
            {
                base.Init(ve, bag, cc);
                ((InfoField)ve).text = text.GetValueFromBag(bag, cc);
            }
        }

        private readonly VisualElement input;
        private readonly Label valueLabel;

        // BaseField adds its own parts before input exists; later children go next to the text.
        public override VisualElement contentContainer => input ?? this;

        /// The read-only text; empty hides it, leaving room for child elements.
        public string text
        {
            get => valueLabel.text;
            set
            {
                valueLabel.text = value;
                valueLabel.style.display = string.IsNullOrEmpty(value) ? DisplayStyle.None : DisplayStyle.Flex;
            }
        }

        public InfoField() : this(null)
        {
        }

        public InfoField(string label) : this(label, new VisualElement())
        {
        }

        private InfoField(string label, VisualElement input) : base(label, input)
        {
            AddToClassList(alignedFieldUssClassName);
            input.AddToClassList("bh-info__input");
            valueLabel = new Label();
            valueLabel.AddToClassList("bh-info__value");
            input.Add(valueLabel);
            this.input = input;
            text = string.Empty;
        }
    }
}

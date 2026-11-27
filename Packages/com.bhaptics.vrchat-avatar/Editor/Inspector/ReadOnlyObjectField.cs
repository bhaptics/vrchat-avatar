using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace bHaptics.VRChatAvatar
{
    /// Leaves an ObjectField showing and pinging its object without
    /// inviting edits: no picker button, and drops refused.
    internal static class ReadOnlyObjectField
    {
        public static void Apply(ObjectField field)
        {
            field.Q(className: ObjectField.selectorUssClassName).style.display = DisplayStyle.None;
            // The field takes a drop in its default action, so prevent it on the way down.
            field.RegisterCallback<DragUpdatedEvent>(e =>
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Rejected;
                e.PreventDefault();
                e.StopImmediatePropagation();
            }, TrickleDown.TrickleDown);
            field.RegisterCallback<DragPerformEvent>(e =>
            {
                e.PreventDefault();
                e.StopImmediatePropagation();
            }, TrickleDown.TrickleDown);
        }
    }
}

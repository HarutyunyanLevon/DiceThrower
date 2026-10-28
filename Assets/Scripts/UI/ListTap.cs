using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace DiceThrower.UI
{
    /// <summary>
    /// A tap for rows inside a ScrollView. Unlike PressGesture it never captures the pointer, so a drag
    /// still scrolls the list and then no tap fires.
    /// </summary>
    public static class ListTap
    {
        private const string PressedClass = "pressed";
        private const int NoPointer = -1;

        /// <param name="slopPx">Pointer travel (UI px) after which the press is a drag, not a tap.</param>
        /// <param name="consume">True for buttons inside a row: the row underneath does not see the press.</param>
        public static void Attach(VisualElement el, Action onTap, float slopPx, bool consume)
        {
            int pointer = NoPointer;
            Vector2 start = Vector2.zero;

            Action cancel = () =>
            {
                pointer = NoPointer;
                el.RemoveFromClassList(PressedClass);
            };

            el.RegisterCallback<PointerDownEvent>(e =>
            {
                pointer = e.pointerId;
                start = e.position;
                el.AddToClassList(PressedClass);
                if (consume) e.StopPropagation();
            });
            el.RegisterCallback<PointerMoveEvent>(e =>
            {
                if (pointer == e.pointerId && ((Vector2)e.position - start).magnitude > slopPx) cancel();
            });
            el.RegisterCallback<PointerUpEvent>(e =>
            {
                if (pointer != e.pointerId) return;
                cancel();
                onTap();
                if (consume) e.StopPropagation();
            });
            el.RegisterCallback<PointerLeaveEvent>(e => cancel());
            el.RegisterCallback<PointerCancelEvent>(e => cancel());
        }
    }
}

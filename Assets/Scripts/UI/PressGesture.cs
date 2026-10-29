using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace DiceThrower.UI
{
    /// <summary>
    /// Tap, or press-and-slide vertically. A tap fires OnTap on release. Moving more than the
    /// threshold turns it into a slide: sliding up raises the value (OnSlide), release commits it.
    /// Holding still for LongPressMillis fires OnLongPress instead (when it is set); the release that follows does nothing.
    /// </summary>
    public sealed class PressGesture
    {
        public int Min;
        public int Max;
        public float PxPerStep;
        public float SlideThreshold;

        public Func<int> GetStart;
        public Action OnTap;
        public Action OnSlideBegin;
        public Action<int> OnSlide;
        public Action<int> OnCommit;
        public Action OnEnd;
        public Action OnLongPress;
        /// <summary>How long a press has to be held still before OnLongPress fires.</summary>
        public long LongPressMillis;

        private readonly VisualElement _el;
        private bool _down;
        private bool _sliding;
        private int _pointer;
        private float _startY;
        private int _startValue;
        private int _value;
        private IVisualElementScheduledItem _longPress;

        public PressGesture(VisualElement el)
        {
            _el = el;
            el.RegisterCallback<PointerDownEvent>(OnDown);
            el.RegisterCallback<PointerMoveEvent>(OnMove);
            el.RegisterCallback<PointerUpEvent>(OnUp);
            el.RegisterCallback<PointerCancelEvent>(e => Cancel());
            el.RegisterCallback<PointerCaptureOutEvent>(e => Cancel());
        }

        /// <summary>Convenience: a plain tappable element.</summary>
        public static PressGesture Tappable(VisualElement el, Action onTap)
        {
            return new PressGesture(el) { OnTap = onTap };
        }

        private void OnDown(PointerDownEvent e)
        {
            if (_down) return;
            _down = true;
            _sliding = false;
            _pointer = e.pointerId;
            _startY = e.position.y;
            _startValue = GetStart != null ? GetStart() : 0;
            _value = _startValue;
            _el.CapturePointer(e.pointerId);
            _el.AddToClassList("pressed");
            if (OnLongPress != null) _longPress = _el.schedule.Execute(FireLongPress).StartingIn(LongPressMillis);
            e.StopPropagation();
        }

        private void OnMove(PointerMoveEvent e)
        {
            if (!_down || e.pointerId != _pointer || OnSlide == null) return;
            float dy = _startY - e.position.y; // up is positive
            if (!_sliding)
            {
                if (Mathf.Abs(dy) < SlideThreshold) return;
                _sliding = true;
                CancelLongPress();
                if (OnSlideBegin != null) OnSlideBegin();
            }
            int v = Mathf.Clamp(_startValue + Mathf.RoundToInt(dy / PxPerStep), Min, Max);
            if (v != _value)
            {
                _value = v;
                OnSlide(v);
            }
            e.StopPropagation();
        }

        private void OnUp(PointerUpEvent e)
        {
            if (!_down || e.pointerId != _pointer) return;
            bool wasSliding = _sliding;
            int value = _value;
            Reset();
            _el.ReleasePointer(e.pointerId);
            if (wasSliding)
            {
                if (OnCommit != null) OnCommit(value);
                if (OnEnd != null) OnEnd();
            }
            else if (OnTap != null)
            {
                OnTap();
            }
            e.StopPropagation();
        }

        private void Cancel()
        {
            if (!_down) return;
            bool wasSliding = _sliding;
            Reset();
            if (wasSliding && OnEnd != null) OnEnd();
        }

        /// <summary>The press was held still long enough: the gesture ends here, so neither a tap nor a slide follows.</summary>
        private void FireLongPress()
        {
            if (!_down || _sliding) return;
            int pointer = _pointer;
            Reset();
            _el.ReleasePointer(pointer);
            if (OnLongPress != null) OnLongPress();
        }

        private void CancelLongPress()
        {
            if (_longPress == null) return;
            _longPress.Pause();
            _longPress = null;
        }

        private void Reset()
        {
            _down = false;
            _sliding = false;
            CancelLongPress();
            _el.RemoveFromClassList("pressed");
        }
    }

    /// <summary>The floating vertical track + value bubble shown while a press-and-slide is active.</summary>
    public sealed class SliderOverlay
    {
        private readonly VisualElement _parent;
        private readonly VisualElement _track;
        private readonly VisualElement _bubble;
        private readonly Label _text;
        private readonly float _pad;
        private Vector2 _anchor;
        private int _start;
        private float _step;
        private Func<int, string> _format;

        public SliderOverlay(VisualElement parent, float trackPad)
        {
            _parent = parent;
            _pad = trackPad;
            _track = new VisualElement();
            _track.pickingMode = PickingMode.Ignore;
            _track.AddToClassList("slider-track");
            _bubble = new VisualElement();
            _bubble.pickingMode = PickingMode.Ignore;
            _bubble.AddToClassList("slider-bubble");
            _text = new Label();
            _text.pickingMode = PickingMode.Ignore;
            _text.AddToClassList("lbl");
            _text.AddToClassList("slider-bubble__text");
            _bubble.Add(_text);
            _track.style.display = DisplayStyle.None;
            _bubble.style.display = DisplayStyle.None;
            parent.Add(_track);
            parent.Add(_bubble);
        }

        public void Show(VisualElement anchorElement, int min, int max, int start, float pxPerStep, Func<int, string> format)
        {
            _anchor = _parent.WorldToLocal(anchorElement.worldBound.center);
            _start = start;
            _step = pxPerStep;
            _format = format;

            // width and centring (translate) come from USS; only the position and length depend on the slide
            _track.style.left = _anchor.x;
            _track.style.top = _anchor.y - (max - start) * pxPerStep - _pad;
            _track.style.height = (max - min) * pxPerStep + _pad * 2f;
            _track.style.display = DisplayStyle.Flex;
            _bubble.style.display = DisplayStyle.Flex;
            Update(start);
        }

        public void Update(int value)
        {
            _text.text = _format(value);
            _bubble.style.left = _anchor.x;
            _bubble.style.top = _anchor.y - (value - _start) * _step;
        }

        public void Hide()
        {
            _track.style.display = DisplayStyle.None;
            _bubble.style.display = DisplayStyle.None;
        }
    }
}

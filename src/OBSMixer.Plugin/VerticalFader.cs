using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace OBSMixer
{
    public sealed class VerticalFader : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler, IEndDragHandler, IInitializePotentialDragHandler
    {
        private RectTransform rect, thumb;
        private Image target;
        private Action<float> changed;
        private Action began, ended;
        private bool interactable, dragging;
        private float value;
        internal void Initialize(Action begin, Action<float> change, Action end)
        {
            rect = (RectTransform)transform; began = begin; changed = change; ended = end;
            target = CurvedGraphics.Image("Fader target", rect, new Color(.1f, .15f, .22f, .02f), true);
            target.rectTransform.anchorMin = Vector2.zero; target.rectTransform.anchorMax = Vector2.one;
            target.rectTransform.offsetMin = target.rectTransform.offsetMax = Vector2.zero;
            var track = CurvedGraphics.Image("Track", rect, new Color(.2f, .3f, .4f, 1));
            track.rectTransform.anchorMin = new Vector2(.5f, 0); track.rectTransform.anchorMax = new Vector2(.5f, 1);
            track.rectTransform.offsetMin = new Vector2(-.325f, 3); track.rectTransform.offsetMax = new Vector2(.325f, -3);
            thumb = CurvedGraphics.Image("Fader handle", rect, new Color(.3f, .75f, 1, 1)).rectTransform;
            thumb.anchorMin = thumb.anchorMax = new Vector2(.5f, 0);
            thumb.sizeDelta = new Vector2(Mathf.Min(6, Mathf.Max(2.5f, rect.rect.width - 1)), 2.5f);
            SetValue(0);
        }
        internal void SetInteractable(bool allowed)
        {
            interactable = allowed;
            target.raycastTarget = allowed;
            if (!allowed) Finish();
        }
        internal void SetValue(float position)
        {
            if (dragging) return;
            value = Mathf.Clamp01(position); Draw();
        }
        private void Draw() => thumb.anchoredPosition = new Vector2(0, 3 + value * Mathf.Max(1, rect.rect.height - 6));
        private void Read(PointerEventData data)
        {
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, data.position, data.pressEventCamera, out var point)) return;
            if (float.IsNaN(point.y) || float.IsInfinity(point.y)) return;
            value = Mathf.Clamp01((point.y - rect.rect.yMin - 3) / Mathf.Max(1, rect.rect.height - 6));
            Draw(); changed?.Invoke(value);
        }
        public void OnInitializePotentialDrag(PointerEventData data) { data.useDragThreshold = false; }
        public void OnPointerDown(PointerEventData data)
        {
            if (!interactable || data.button != PointerEventData.InputButton.Left) return;
            dragging = true; began?.Invoke(); Read(data);
        }
        public void OnDrag(PointerEventData data) { if (dragging) Read(data); }
        public void OnPointerUp(PointerEventData data) { if (dragging) { Read(data); Finish(); } }
        public void OnEndDrag(PointerEventData data) => Finish();
        private void Finish() { if (!dragging) return; dragging = false; ended?.Invoke(); }
        private void OnDisable() => Finish();
    }
}

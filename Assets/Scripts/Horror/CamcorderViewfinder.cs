using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DayOfDream.Horror
{
    public sealed class CamcorderViewfinder : MonoBehaviour
    {
        [SerializeField] private CanvasGroup canvas;
        [SerializeField] private TMP_Text timer, mode;
        [SerializeField] private Graphic recordDot;
        [SerializeField] private RectTransform zoomMarker;
        [SerializeField] private float zoomTrackWidth = 220;
        private int lastSecond = -1;
        private RawImage liveFeed;
        private CanvasGroup markings;

        internal void SetFeed(Texture texture)
        {
            if (liveFeed == null)
            {
                var frame = new GameObject("DV Readouts", typeof(RectTransform), typeof(CanvasGroup));
                var frameRect = (RectTransform)frame.transform;
                frameRect.SetParent(canvas.transform, false);
                Stretch(frameRect);
                for (int i = canvas.transform.childCount - 2; i >= 0; --i)
                {
                    var child = canvas.transform.GetChild(i);
                    child.SetParent(frameRect, false);
                    child.SetAsFirstSibling();
                }
                markings = frame.GetComponent<CanvasGroup>();
                var video = new GameObject("DV Optical Feed", typeof(RectTransform), typeof(RawImage));
                liveFeed = video.GetComponent<RawImage>();
                liveFeed.raycastTarget = false;
                liveFeed.rectTransform.SetParent(canvas.transform, false);
                liveFeed.rectTransform.SetAsFirstSibling();
                Stretch(liveFeed.rectTransform);
            }
            liveFeed.texture = texture;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        public void Present(float alpha, bool infrared, float zoom, float seconds)
        {
            canvas.alpha = alpha;
            bool visible = alpha > 0;
            if (canvas.gameObject.activeSelf != visible) canvas.gameObject.SetActive(visible);
            if (!visible) return;
            if (markings != null) markings.alpha = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.45f, .92f, alpha));

            int wholeSeconds = (int)seconds;
            if (lastSecond != wholeSeconds)
            {
                lastSecond = wholeSeconds;
                timer.SetText("{0:00}:{1:00}:{2:00}", wholeSeconds / 3600, wholeSeconds / 60 % 60, wholeSeconds % 60);
            }
            string modeLabel = infrared ? "IR  ON" : "IR  OFF";
            if (mode.text != modeLabel) mode.text = modeLabel;
            recordDot.canvasRenderer.SetAlpha(seconds % 1.1f < .76f ? 1 : .17f);
            zoomMarker.anchoredPosition = new Vector2((zoom - .5f) * zoomTrackWidth, zoomMarker.anchoredPosition.y);
        }
    }
}

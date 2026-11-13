using UnityEngine;
using UnityEngine.UI;

namespace DayOfDream.Horror
{
    public sealed class DreamTitleMotion : MonoBehaviour
    {
        [SerializeField] private RectTransform title;
        [SerializeField] private Image[] drops;

        private Vector2 restingPosition;
        private Vector2[] origins;

        private void Awake()
        {
            restingPosition = title.anchoredPosition;
            origins = new Vector2[drops.Length];
            for (int i = 0; i < drops.Length; i++) origins[i] = drops[i].rectTransform.anchoredPosition;
        }

        private void Update()
        {
            float time = Time.unscaledTime;
            title.anchoredPosition = restingPosition + new Vector2(Mathf.Sin(time * .67f) * 1.4f, Mathf.Sin(time * .88f) * 2);
            for (int i = 0; i < drops.Length; i++)
            {
                float phase = Mathf.Repeat(time / (3.1f + i * .83f) + i * .21f, 1);
                float falling = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.47f, 1, phase));
                var drop = drops[i];
                drop.rectTransform.anchoredPosition = origins[i] + Vector2.down * (falling * (42 + i * 12));
                drop.rectTransform.localScale = new Vector3(1 - .36f * falling, .25f + .95f * Mathf.Sin(phase * Mathf.PI), 1);
                drop.canvasRenderer.SetAlpha(Mathf.SmoothStep(0, 1, phase * 5) * (1 - falling));
            }
        }
    }
}

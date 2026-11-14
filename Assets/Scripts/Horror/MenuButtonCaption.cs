using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DayOfDream.Horror
{
    [RequireComponent(typeof(Button))]
    public sealed class MenuButtonCaption : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private Graphic accent, seal;
        private Button button;
        private TMP_Text caption;
        private Vector2 captionPosition;
        private bool hovered;
        private float glow = -1;

        private void Awake()
        {
            button = GetComponent<Button>();
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            var colors = button.colors;
            colors.selectedColor = colors.normalColor;
            button.colors = colors;
            caption = GetComponentInChildren<TMP_Text>();
            captionPosition = caption.rectTransform.anchoredPosition;
        }

        private void OnEnable()
        {
            hovered = false;
            glow = -1;
        }

        private void Update()
        {
            float target = button.interactable && hovered ? 1 : 0;
            if (glow == target) return;
            glow = Mathf.MoveTowards(Mathf.Max(0, glow), target, Time.unscaledDeltaTime * 6);
            caption.color = button.interactable
                ? Color.Lerp(new Color(.78f, .71f, .63f), new Color(1, .93f, .81f), glow)
                : new Color(.45f, .40f, .40f);
            caption.rectTransform.anchoredPosition = captionPosition + Vector2.right * (glow * 9);
            var edge = Color.Lerp(new Color(.30f, .11f, .12f, .64f), new Color(.88f, .26f, .24f), glow);
            if (accent != null) accent.color = edge;
            if (seal != null) seal.color = edge;
        }

        public void OnPointerEnter(PointerEventData eventData) => hovered = true;
        public void OnPointerExit(PointerEventData eventData) => hovered = false;
    }
}

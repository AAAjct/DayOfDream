using UnityEngine;
using TMPro;
using UnityEngine.UI;

namespace DayOfDream.Horror
{
    public sealed class HorrorHud : MonoBehaviour
    {
        [SerializeField] private TMP_Text prompt, noteText;
        [SerializeField] private GameObject pausePanel, notePanel;
        [SerializeField] private GameObject[] hideWhileFilming;
        private InteractionReticle reticle;
        private GameObject reticleRoot;
        private DreamPauseMenu pauseMenu;
        private readonly Image[] inventorySlots = new Image[4];
        private readonly Image[] inventoryIcons = new Image[4];
        private static readonly string[] IconNames = { "CabinetKey", "ExitKey", "Fuse", "Bottle" };
        private bool cinematic;
        private int inventoryMask = -1;
        private int selectedSlot = -1;
        private int displayedMask = -1;
        private int displayedCharge = -1;
        private float throwCharge;
        private float noteUntil;
        public void Configure(TMP_Text interaction, TMP_Text note, GameObject pause, GameObject paper)
        { prompt = interaction; noteText = note; pausePanel = pause; notePanel = paper; }
        private void Awake()
        {
            BuildInventory();
            // Both existing HUDs have a centered TMP reticle, which supplies the layout and color.
            if (transform.Find("Reticle")?.GetComponent<TMP_Text>() is not { } dot) return;
            dot.enabled = false;
            reticleRoot = dot.gameObject;
            var graphic = new GameObject("Interaction reticle", typeof(RectTransform), typeof(InteractionReticle));
            graphic.transform.SetParent(dot.transform, false);
            reticle = graphic.GetComponent<InteractionReticle>();
            reticle.rectTransform.sizeDelta = new Vector2(34, 34);
            reticle.color = dot.color;
            reticle.raycastTarget = false;
        }

        private void BuildInventory()
        {
            for (int i = 0; i < inventorySlots.Length; i++)
            {
                var slot = new GameObject("Inventory Slot", typeof(RectTransform), typeof(Image));
                var image = slot.GetComponent<Image>();
                image.raycastTarget = false;
                RectTransform rect = image.rectTransform;
                rect.SetParent(transform, false);
                rect.anchorMin = rect.anchorMax = new Vector2(.5f, 0);
                rect.anchoredPosition = new Vector2((i - 1.5f) * 116, 80);
                rect.sizeDelta = new Vector2(104, 104);
                rect.SetSiblingIndex(pausePanel.transform.GetSiblingIndex());
                inventorySlots[i] = image;

                var icon = new GameObject("Item image", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                icon.rectTransform.SetParent(rect, false);
                icon.rectTransform.anchorMin = Vector2.zero;
                icon.rectTransform.anchorMax = Vector2.one;
                icon.rectTransform.offsetMin = new Vector2(8, 8);
                icon.rectTransform.offsetMax = new Vector2(-8, -8);
                icon.sprite = Resources.Load<Sprite>("InventoryIcons/" + IconNames[i]);
                icon.preserveAspect = true;
                icon.raycastTarget = false;
                icon.enabled = false;
                inventoryIcons[i] = icon;
            }
            SetInventory(false, false, false);
        }

        internal void SetInventory(bool cabinetKey, bool exitKey, bool fuse, bool bottle = false)
        {
            int mask = (cabinetKey ? 1 : 0) | (exitKey ? 2 : 0) | (fuse ? 4 : 0) | (bottle ? 8 : 0);
            if (mask == inventoryMask) return;
            inventoryMask = mask;
            for (int i = 0; i < inventorySlots.Length; i++)
            {
                bool filled = (mask & (1 << i)) != 0;
                inventorySlots[i].color = filled ? new Color(.16f, .20f, .17f, .93f) : new Color(.07f, .09f, .08f, .72f);
                inventoryIcons[i].enabled = filled;
            }
        }

        internal void SetSelectedSlot(int slot, float charge)
        {
            int percentage = charge > .16f ? Mathf.RoundToInt(charge * 100) : 0;
            if (slot == selectedSlot && inventoryMask == displayedMask && percentage == displayedCharge) return;
            selectedSlot = slot; throwCharge = charge; displayedMask = inventoryMask; displayedCharge = percentage;
            for (int i = 0; i < inventorySlots.Length; i++)
            {
                if (inventorySlots[i] == null) continue;
                bool filled = (inventoryMask & (1 << i)) != 0;
                inventorySlots[i].color = filled && i == selectedSlot ? new Color(.25f, .40f + .2f * throwCharge, .33f, .96f) :
                    filled ? new Color(.16f, .20f, .17f, .93f) : new Color(.07f, .09f, .08f, .72f);

            }
        }

        public void SetPrompt(string value, bool hasTarget = false)
        {
            // Aiming uses only the reticle; outcome feedback and readable notes have their own UI.
            if (prompt != null) prompt.text = "";
            if (reticle != null) reticle.SetFocused(hasTarget);
        }

        internal void SetCinematic(bool value)
        {
            cinematic = value;
            if (reticle != null) reticle.enabled = !value && !pausePanel.activeSelf;
            foreach (var slot in inventorySlots) if (slot != null) slot.gameObject.SetActive(!value);
            SetPrompt("");
        }

        public void SetPaused(bool value)
        {
            pauseMenu?.SetVisible(value);
            if (reticle != null) reticle.enabled = !value && !cinematic;
        }

        internal void BindPauseMenu(FirstPersonExplorer player)
        {
            if (pauseMenu != null) return;
            pausePanel.SetActive(false);
            pauseMenu = new DreamPauseMenu(pausePanel, pausePanel.GetComponentInChildren<TMP_Text>(true),
                () => player.SetPaused(false));
        }

        internal bool TryClosePauseSettings() => pauseMenu?.TryCloseSettings() == true;

        public void SetCamcorderRaised(bool raised)
        {
            if (hideWhileFilming == null) return;
            foreach (GameObject item in hideWhileFilming)
                if (item != null && item != reticleRoot && item.activeSelf == raised)
                    item.SetActive(!raised);
        }
        public void ShowNote(string title, string body, float duration = 10)
        {
            noteText.text = title + "\n\n" + body;
            notePanel.SetActive(true); noteUntil = Time.time + duration;
        }
        internal void DismissNote() { if (notePanel != null) notePanel.SetActive(false); }
        private void Update() { if (notePanel != null && notePanel.activeSelf && Time.time > noteUntil) notePanel.SetActive(false); }
    }
}

using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DayOfDream.Horror
{
    internal sealed class DreamPauseMenu
    {
        private readonly GameObject overlay, menu, settings;
        private readonly TMP_FontAsset font;
        private GameObject ownedEventSystem;

        internal DreamPauseMenu(GameObject overlay, TMP_Text title, Action resume)
        {
            this.overlay = overlay;
            font = title.font;
            title.gameObject.SetActive(false);

            var rect = (RectTransform)overlay.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var dimmer = overlay.GetComponent<Image>();
            dimmer.color = new Color(.01f, .015f, .02f, .74f);
            dimmer.raycastTarget = true;
            // Keep pause controls usable over the eyelids during execution or focus loss.
            var canvas = overlay.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = 110;
            overlay.AddComponent<GraphicRaycaster>();

            menu = Card(rect, "Pause Menu", new Vector2(490, 392));
            Label(menu.transform, "暂停", new Vector2(0, 132), new Vector2(420, 52), 42);
            var settingsButton = CreateButton(menu.transform, "设置", new Vector2(0, 53), 330);
            var resumeButton = CreateButton(menu.transform, "返回游戏", new Vector2(0, -29), 330);
            var mainMenuButton = CreateButton(menu.transform, "返回主菜单", new Vector2(0, -111), 330);
            settingsButton.onClick.AddListener(() => ShowSettings(true));
            resumeButton.onClick.AddListener(() => resume());
            mainMenuButton.onClick.AddListener(() => SceneManager.LoadScene(DreamMainMenu.SceneName));

            settings = Card(rect, "Pause Settings", new Vector2(720, 510));
            Label(settings.transform, "设置", new Vector2(0, 189), new Vector2(640, 52), 42);
            Label(settings.transform, "主音量", new Vector2(-184, 110), new Vector2(245, 44), 28);
            Label(settings.transform, "鼠标灵敏度", new Vector2(-184, 38), new Vector2(245, 44), 28);
            Label(settings.transform, "帧率限制", new Vector2(-184, -34), new Vector2(245, 44), 28);
            Label(settings.transform, "垂直同步", new Vector2(-184, -106), new Vector2(245, 44), 28);
            var volume = CreateSlider(settings.transform, "Volume", new Vector2(127, 110), 0, 1);
            var sensitivity = CreateSlider(settings.transform, "Sensitivity", new Vector2(127, 38),
                DreamSettings.MinSensitivity, DreamSettings.MaxSensitivity);
            var frameRate = CreateButton(settings.transform, "帧率限制", new Vector2(127, -34), 280);
            var vSync = CreateButton(settings.transform, "垂直同步", new Vector2(127, -106), 280);
            var back = CreateButton(settings.transform, "返回", new Vector2(0, -184), 330);
            back.onClick.AddListener(() => ShowSettings(false));
            new DreamSettings(volume, sensitivity, frameRate, vSync);
            settings.SetActive(false);
        }

        internal void SetVisible(bool visible)
        {
            if (overlay.activeSelf == visible) return;
            if (visible)
            {
                bool needsEventSystem = EventSystem.current == null;
                if (needsEventSystem && ownedEventSystem == null)
                {
                    ownedEventSystem = new GameObject("Pause EventSystem");
                    ownedEventSystem.transform.SetParent(overlay.transform, false);
                    ownedEventSystem.AddComponent<EventSystem>();
                    ownedEventSystem.AddComponent<InputSystemUIInputModule>();
                }
                if (ownedEventSystem != null) ownedEventSystem.SetActive(needsEventSystem);
                overlay.SetActive(true);
                EventSystem.current?.SetSelectedGameObject(null);
            }
            else
            {
                if (settings.activeSelf) ShowSettings(false);
                overlay.SetActive(false);
            }
        }

        internal bool TryCloseSettings()
        {
            if (!settings.activeSelf) return false;
            ShowSettings(false);
            return true;
        }

        private void ShowSettings(bool visible)
        {
            menu.SetActive(!visible);
            settings.SetActive(visible);
            EventSystem.current?.SetSelectedGameObject(null);
            if (!visible) PlayerPrefs.Save();
        }

        private static GameObject Card(Transform parent, string name, Vector2 size)
        {
            var card = new GameObject(name, typeof(RectTransform), typeof(Image));
            var image = card.GetComponent<Image>();
            Place(image.rectTransform, parent, Vector2.zero, size);
            image.color = new Color(.035f, .047f, .053f);
            return card;
        }

        private TMP_Text Label(Transform parent, string text, Vector2 position, Vector2 size, float fontSize)
        {
            var label = new GameObject(text, typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            Place(label.rectTransform, parent, position, size);
            label.font = font;
            label.text = text;
            label.fontSize = fontSize;
            label.alignment = TextAlignmentOptions.Center;
            label.color = new Color(.88f, .91f, .88f);
            label.raycastTarget = false;
            return label;
        }

        private Button CreateButton(Transform parent, string text, Vector2 position, float width)
        {
            var button = new GameObject(text, typeof(RectTransform), typeof(Image), typeof(Button)).GetComponent<Button>();
            button.targetGraphic = button.GetComponent<Image>();
            var size = new Vector2(width, 56);
            Place((RectTransform)button.transform, parent, position, size);
            Label(button.transform, text, Vector2.zero, size, 28);
            var colors = button.colors;
            colors.normalColor = new Color(.13f, .078f, .087f);
            colors.highlightedColor = new Color(.29f, .09f, .112f);
            colors.selectedColor = colors.normalColor;
            colors.pressedColor = new Color(.46f, .13f, .14f);
            colors.disabledColor = new Color(.075f, .059f, .066f);
            button.colors = colors;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            return button;
        }

        private static Slider CreateSlider(Transform parent, string name, Vector2 position, float min, float max)
        {
            var slider = DefaultControls.CreateSlider(default).GetComponent<Slider>();
            slider.name = name;
            Place((RectTransform)slider.transform, parent, position, new Vector2(280, 48));
            slider.minValue = min;
            slider.maxValue = max;
            slider.fillRect.GetComponent<Image>().color = new Color(.55f, .25f, .30f);
            return slider;
        }

        private static void Place(RectTransform rect, Transform parent, Vector2 position, Vector2 size)
        {
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }
    }
}

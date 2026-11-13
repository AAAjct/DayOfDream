using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering.Universal;

namespace DayOfDream.Horror
{
    public sealed class DreamMainMenu : MonoBehaviour
    {
        [SerializeField] private Button startButton, continueButton, settingsButton, quitButton, backButton;
        [SerializeField] private GameObject settingsPanel;
        [SerializeField] private Slider volumeSlider, sensitivitySlider;

        private void Awake()
        {
            Time.timeScale = 1;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            AudioListener.volume = PlayerPrefs.GetFloat("Dream.Volume", .8f);
            if (Camera.main is { } camera)
            {
                var cameraData = camera.GetUniversalAdditionalCameraData();
                cameraData.renderPostProcessing = true;
                cameraData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            }
            startButton.onClick.AddListener(StartGame);
            continueButton.onClick.AddListener(ContinueGame);
            continueButton.interactable = DreamProgress.HasSave;
            settingsButton.onClick.AddListener(() => ShowSettings(true));
            backButton.onClick.AddListener(() => ShowSettings(false));
            quitButton.onClick.AddListener(Quit);
            volumeSlider.SetValueWithoutNotify(AudioListener.volume);
            sensitivitySlider.SetValueWithoutNotify(PlayerPrefs.GetFloat("Dream.Sensitivity", 1));
            volumeSlider.onValueChanged.AddListener(value => { AudioListener.volume = value; PlayerPrefs.SetFloat("Dream.Volume", value); });
            sensitivitySlider.onValueChanged.AddListener(value => PlayerPrefs.SetFloat("Dream.Sensitivity", value));
            settingsPanel.SetActive(false);
        }

        private void Update()
        {
            if (settingsPanel.activeSelf && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                ShowSettings(false);

            var events = EventSystem.current;
            if (events == null || events.currentSelectedGameObject != null) return;
            if (events.currentInputModule is InputSystemUIInputModule input &&
                input.move?.action is { } move && move.WasPerformedThisFrame() &&
                move.ReadValue<Vector2>().sqrMagnitude > .25f)
                events.SetSelectedGameObject((settingsPanel.activeSelf ? backButton : startButton).gameObject);
        }

        public void StartGame() { DreamProgress.NewGame(); SceneManager.LoadScene("AnonHouse"); }
        public void ContinueGame()
        {
            if (!DreamProgress.HasSave) return;
            DreamProgress.RequestContinue();
            SceneManager.LoadScene("AnonHouse");
        }
        private void ShowSettings(bool visible)
        {
            settingsPanel.SetActive(visible);
            EventSystem.current?.SetSelectedGameObject(null);
            if (!visible) PlayerPrefs.Save();
        }
        public void Quit()
        {
            PlayerPrefs.Save();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}

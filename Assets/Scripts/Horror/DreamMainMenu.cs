using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;

namespace DayOfDream.Horror
{
    public sealed class DreamMainMenu : MonoBehaviour
    {
        internal const string SceneName = "MainMenu";

        [SerializeField] private Button startButton, continueButton, settingsButton, quitButton, backButton;
        [SerializeField] private GameObject settingsPanel;
        [SerializeField] private Slider volumeSlider, sensitivitySlider;
        [SerializeField] private Button frameRateButton, vSyncButton;

        private void Awake()
        {
            Time.timeScale = 1;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            AudioListener.volume = DreamSettings.Volume;
            if (Camera.main is { } camera)
            {
                var cameraData = camera.GetUniversalAdditionalCameraData();
                cameraData.renderPostProcessing = true;
                cameraData.antialiasing = Application.isMobilePlatform
                    ? AntialiasingMode.FastApproximateAntialiasing
                    : AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            }
            startButton.onClick.AddListener(StartGame);
            continueButton.onClick.AddListener(ContinueGame);
            continueButton.interactable = DreamProgress.HasSave;
            settingsButton.onClick.AddListener(() => ShowSettings(true));
            backButton.onClick.AddListener(() => ShowSettings(false));
            quitButton.onClick.AddListener(Quit);
            new DreamSettings(volumeSlider, sensitivitySlider, frameRateButton, vSyncButton);
            settingsPanel.SetActive(false);
        }

        private void Update()
        {
            if (settingsPanel.activeSelf && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                ShowSettings(false);
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

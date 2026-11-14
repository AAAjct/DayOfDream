using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DayOfDream.Horror
{
    internal sealed class DreamSettings
    {
        private const string VolumeKey = "Dream.Volume";
        private const string SensitivityKey = "Dream.Sensitivity";

        internal const float MinSensitivity = .25f;
        internal const float MaxSensitivity = 2.5f;
        internal static float Volume => PlayerPrefs.GetFloat(VolumeKey, .8f);
        internal static float Sensitivity => PlayerPrefs.GetFloat(SensitivityKey, 1f);

        private readonly Button frameRate, vSync;
        private readonly TMP_Text frameRateCaption, vSyncCaption;

        internal DreamSettings(Slider volume, Slider sensitivity, Button frameRate, Button vSync)
        {
            this.frameRate = frameRate;
            this.vSync = vSync;
            frameRateCaption = frameRate.GetComponentInChildren<TMP_Text>(true);
            vSyncCaption = vSync.GetComponentInChildren<TMP_Text>(true);

            volume.navigation = sensitivity.navigation = new Navigation { mode = Navigation.Mode.None };
            volume.SetValueWithoutNotify(Volume);
            sensitivity.SetValueWithoutNotify(Sensitivity);
            volume.onValueChanged.AddListener(value =>
            {
                AudioListener.volume = value;
                PlayerPrefs.SetFloat(VolumeKey, value);
            });
            sensitivity.onValueChanged.AddListener(value => PlayerPrefs.SetFloat(SensitivityKey, value));
            frameRate.onClick.AddListener(CycleFrameRate);
            vSync.onClick.AddListener(ToggleVSync);
            RefreshDisplaySettings();
        }

        private void CycleFrameRate()
        {
            DreamFrameRate.CycleLimit();
            RefreshDisplaySettings();
            PlayerPrefs.Save();
        }

        private void ToggleVSync()
        {
            DreamFrameRate.ToggleVSync();
            RefreshDisplaySettings();
            PlayerPrefs.Save();
        }

        private void RefreshDisplaySettings()
        {
            int limit = DreamFrameRate.Limit;
            bool useVSync = DreamFrameRate.VSyncEnabled;
            frameRateCaption.text = limit == -1 ? "无限制" : $"{limit} FPS";
            frameRate.interactable = !useVSync;
            vSyncCaption.text = useVSync ? "开启" : "关闭";
        }
    }
}

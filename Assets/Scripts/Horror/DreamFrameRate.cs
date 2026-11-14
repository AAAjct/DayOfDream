using System;
using UnityEngine;

namespace DayOfDream.Horror
{
    internal static class DreamFrameRate
    {
        private const string LimitKey = "Dream.FrameRateLimit";
        private const string VSyncKey = "Dream.VSync";
        private const int DesktopDefaultLimit = 120;
        private const int MobileDefaultLimit = 60;
        private static readonly int[] Limits = { 30, 60, 90, 120, -1 };

        internal static int Limit
        {
            get
            {
                int defaultLimit = Application.isMobilePlatform ? MobileDefaultLimit : DesktopDefaultLimit;
                int savedLimit = PlayerPrefs.GetInt(LimitKey, defaultLimit);
                return Array.IndexOf(Limits, savedLimit) >= 0 ? savedLimit : defaultLimit;
            }
        }

        internal static bool VSyncEnabled => PlayerPrefs.GetInt(VSyncKey, 0) == 1;

        internal static void CycleLimit()
        {
            int next = (Array.IndexOf(Limits, Limit) + 1) % Limits.Length;
            PlayerPrefs.SetInt(LimitKey, Limits[next]);
            Apply();
        }

        internal static void ToggleVSync()
        {
            PlayerPrefs.SetInt(VSyncKey, VSyncEnabled ? 0 : 1);
            Apply();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Apply()
        {
            QualitySettings.vSyncCount = VSyncEnabled ? 1 : 0;
            Application.targetFrameRate = Limit;
        }
    }
}

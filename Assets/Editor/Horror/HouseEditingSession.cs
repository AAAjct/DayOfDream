using UnityEditor;
namespace DayOfDream.Editor
{
    // Short-lived ownership of the Editor while applying multi-step scene upgrades.
    [InitializeOnLoad]
    public static class HouseEditingSession
    {
        private const string Key = "DayOfDream.HouseEditingSession";
        static HouseEditingSession()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.ExitingEditMode && SessionState.GetBool(Key,false))
                    EditorApplication.isPlaying = false;
            };
        }
        public static void Begin() { SessionState.SetBool(Key,true); EditorApplication.isPlaying=false; }
        public static void End() => SessionState.SetBool(Key,false);
    }
}

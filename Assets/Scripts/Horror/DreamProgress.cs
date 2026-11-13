using System;
using System.IO;
using UnityEngine;

namespace DayOfDream.Horror
{
    // A single local exploration checkpoint. Only safe, standing gameplay is saved.
    public static class DreamProgress
    {
        [Serializable] public sealed class Item
        {
            public string id;
            public Vector3 position;
            public Quaternion rotation;
            public bool active;
        }
        [Serializable] public sealed class Snapshot
        {
            public int version = 1, day;
            public bool powered, cabinetOpen, dv;
            public Vector3 position;
            public Quaternion rotation;
            public string[] slots;
            public Item[] items;
        }
        private static string Path => System.IO.Path.Combine(Application.persistentDataPath, "dream-checkpoint.json");
        public static bool HasSave => Read() != null;
        public static void NewGame()
        {
            PlayerPrefs.DeleteKey("Dream.Continue");
            if (File.Exists(Path)) File.Delete(Path);
        }
        public static void RequestContinue() => PlayerPrefs.SetInt("Dream.Continue", 1);
        internal static Snapshot ConsumeContinue()
        {
            if (PlayerPrefs.GetInt("Dream.Continue", 0) != 1) return null;
            PlayerPrefs.DeleteKey("Dream.Continue");
            return Read();
        }
        private static Snapshot Read()
        {
            try
            {
                if (!File.Exists(Path)) return null;
                var save = JsonUtility.FromJson<Snapshot>(File.ReadAllText(Path));
                return save != null && save.version == 1 && save.slots?.Length == 4 && save.items != null ? save : null;
            }
            catch (Exception e) when (e is IOException || e is ArgumentException) { return null; }
        }
        internal static void Write(Snapshot snapshot)
        {
            try
            {
                string temporary = Path + ".tmp";
                File.WriteAllText(temporary, JsonUtility.ToJson(snapshot));
                if (File.Exists(Path)) File.Replace(temporary, Path, null);
                else File.Move(temporary, Path);
            }
            catch (IOException e) { Debug.LogWarning("无法保存游戏进度：" + e.Message); }
        }
    }
}

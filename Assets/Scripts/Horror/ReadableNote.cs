using UnityEngine;

namespace DayOfDream.Horror
{
    public sealed class ReadableNote : MonoBehaviour
    {
        [SerializeField] private string title;
        [SerializeField, TextArea(3, 10)] private string body;
        public string Title => title;
        public string Body => body;
        public void Configure(string heading, string content) { title = heading; body = content; }
    }
}

using UnityEngine;

namespace DayOfDream.Horror
{
    public sealed class HandheldFlashlight : MonoBehaviour
    {
        [SerializeField] private Light beam;
        [SerializeField] private Transform prop;
        [SerializeField] private FirstPersonExplorer explorer;
        private bool requestedOn, stowed;
        public bool IsOn => beam != null && beam.enabled;
        public void Configure(Light spot, Transform model, FirstPersonExplorer player)
        { beam = spot; prop = model; explorer = player; }
        private void Awake() => requestedOn = IsOn;
        public void Toggle() => SetOn(!IsOn);

        public void SetOn(bool value)
        {
            requestedOn = value;
            if (beam != null) beam.enabled = requestedOn && !stowed;
        }

        public void SetStowed(bool value)
        {
            if (stowed == value) return;
            if (!stowed) requestedOn = IsOn;
            stowed = value;
            if (prop != null) prop.gameObject.SetActive(!stowed);
            if (beam != null) beam.enabled = requestedOn && !stowed;
        }
    }
}

using UnityEngine;
using UnityEngine.InputSystem;

namespace DayOfDream.Horror
{
    public sealed class LeftHandInventory : MonoBehaviour
    {
        private AnonHouse house;
        private Transform eye, grip;
        private HorrorHud hud;
        private ExplorerAudio audioFeedback;
        private InputAction drop;
        private InputAction[] slots = new InputAction[4];
        private HouseObject held;
        private bool charging;
        private float charge;
        internal int SelectedSlot { get; private set; }
        internal float Charge => charge;

        internal void Initialize(AnonHouse owner, Transform view, HorrorHud display, ExplorerAudio sounds, InputActionMap map)
        {
            house = owner; eye = view; hud = display; audioFeedback = sounds;
            drop = map.AddAction("Drop item", InputActionType.Button, "<Keyboard>/q");
            for (int i = 0; i < slots.Length; i++)
                slots[i] = map.AddAction("Left hand slot " + i, InputActionType.Button, "<Keyboard>/" + (i + 1));
            grip = new GameObject("Left hand item").transform;
            grip.SetParent(eye, false);
            grip.localPosition = new Vector3(-.28f, -.24f, .47f);
            grip.localRotation = Quaternion.Euler(-15, 15, 70);

        }

        internal void Tick(bool available)
        {
            if (house == null) return;
            if (!available) { Cancel(); if (grip != null) grip.gameObject.SetActive(false); return; }
            grip.gameObject.SetActive(true);
            for (int i = 0; i < slots.Length; i++)
                if (slots[i].WasPressedThisFrame()) { SelectedSlot = i; Cancel(); }
            var item = house.GetSlot(SelectedSlot);
            if (held != item)
            {
                if (held != null && held.IsHeld) held.Stow();
                held = item;
                if (held != null) held.Hold(grip);
                Cancel();
            }
            hud.SetSelectedSlot(SelectedSlot, charge);
            if (held == null) return;
            if (drop.WasPressedThisFrame()) { charging = true; charge = 0; }
            if (charging && drop.IsPressed())
            {
                charge = Mathf.Min(1, charge + Time.deltaTime / 1.25f);
            }
            if (charging && drop.WasReleasedThisFrame())
            {
                Vector3 velocity = Velocity();
                HouseObject thrown = house.ReleaseSlot(SelectedSlot);
                if (thrown != null) { thrown.Launch(Origin(), velocity, house); audioFeedback.Throw(); }
                held = null;
                Cancel();
            }
        }

        private Vector3 Origin()
        {
            Vector3 offset = eye.TransformDirection(new Vector3(-.20f, -.12f, .5f));
            float length = offset.magnitude;
            if (Physics.SphereCast(eye.position, .12f, offset.normalized, out var hit, length, ~(1 << 2), QueryTriggerInteraction.Ignore))
                length = Mathf.Max(0, hit.distance - .04f);
            return eye.position + offset.normalized * length;
        }
        private Vector3 Velocity() => charge <= .16f ? eye.forward * 1.3f :
            eye.forward * Mathf.Lerp(3, 13, charge) + Vector3.up * Mathf.Lerp(1.2f, 4, charge);

        internal void Cancel()
        {
            charging = false; charge = 0;
            if (hud != null) hud.SetSelectedSlot(SelectedSlot, 0);
        }
        private void OnDisable() => Cancel();
    }
}

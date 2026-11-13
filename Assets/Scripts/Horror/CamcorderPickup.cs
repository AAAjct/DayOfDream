using UnityEngine;

namespace DayOfDream.Horror
{
    [RequireComponent(typeof(BoxCollider))]
    public sealed class CamcorderPickup : MonoBehaviour
    {
        internal void TryPickUp(HandheldCamcorder camcorder)
        {
            if (camcorder.TryEquip(this)) gameObject.SetActive(false);
        }
    }
}

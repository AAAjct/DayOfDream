using UnityEngine;

namespace DayOfDream.Horror
{
    // Only the optical camera receives sensor processing; the player's eyes stay unfiltered.
    internal sealed class CamcorderSensorCamera : MonoBehaviour
    {
        internal HandheldCamcorder Owner { get; set; }
    }
}

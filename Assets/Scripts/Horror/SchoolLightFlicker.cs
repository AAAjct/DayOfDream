using UnityEngine;

namespace DayOfDream.Horror
{
    [RequireComponent(typeof(Light))]
    public sealed class SchoolLightFlicker : MonoBehaviour
    {
        [SerializeField] private Renderer fixture;
        private MaterialPropertyBlock properties;
        private Color emission;
        private Light lamp;
        private float baseline, seed;
        private void Awake()
        {
            lamp = GetComponent<Light>(); baseline = lamp.intensity; seed = transform.position.x * 7.3f + 42;
            if (fixture != null) { properties = new MaterialPropertyBlock(); emission = fixture.sharedMaterial.GetColor("_EmissionColor"); }
        }
        private void Update()
        {
            float t = Time.time;
            float flutter = Mathf.PerlinNoise(seed, t * 10);
            lamp.intensity = baseline * (flutter < .28f ? .08f : .88f + .12f * flutter);
            if (fixture != null)
            {
                properties.SetColor("_EmissionColor", emission * (lamp.intensity / Mathf.Max(.001f, baseline)));
                fixture.SetPropertyBlock(properties);
            }
        }
    }
}

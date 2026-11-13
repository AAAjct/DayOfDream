using UnityEngine;
using UnityEngine.Rendering;

namespace DayOfDream.Horror
{
    public sealed class TomorinFirstPersonBody : MonoBehaviour
    {
        [SerializeField] private FirstPersonExplorer explorer;
        [SerializeField] private Transform leftLeg, rightLeg;
        private Quaternion leftRest, rightRest;
        private Vector3 rest;
        private float phase;
        public void Configure(FirstPersonExplorer player, Transform left, Transform right)
        { explorer = player; leftLeg = left; rightLeg = right; }
        private void Awake()
        {
            foreach (var renderer in GetComponentsInChildren<Renderer>(true))
                renderer.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
            rest = transform.localPosition;
            if (leftLeg != null) leftRest = leftLeg.localRotation;
            if (rightLeg != null) rightRest = rightLeg.localRotation;
        }
        private void LateUpdate()
        {
            if (explorer == null || explorer.IsPaused) return;
            phase += explorer.MovementSpeed * Time.deltaTime * 3;
            float swing = Mathf.Sin(phase) * Mathf.Min(19, explorer.MovementSpeed * 6);
            if (leftLeg != null) leftLeg.localRotation = leftRest * Quaternion.Euler(swing, 0, 0);
            if (rightLeg != null) rightLeg.localRotation = rightRest * Quaternion.Euler(-swing, 0, 0);
            transform.localPosition = Vector3.Lerp(transform.localPosition, rest + Vector3.down * (explorer.IsCrouched ? .48f : 0), Time.deltaTime * 12);
        }
    }
}

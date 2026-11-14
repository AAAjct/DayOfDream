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
            float left = swing;
            float right = -swing;
            float drop = explorer.IsCrouched ? .48f : 0f;
            float progress = explorer.TraversalProgress;
            if (explorer.IsClimbing)
            {
                left = -63f * PoseWeight(progress, .06f, .34f, .53f, .79f);
                right = -52f * PoseWeight(progress, .27f, .54f, .68f, .96f);
                drop = .32f * PoseWeight(progress, 0f, .25f, .65f, 1f);
            }
            else if (explorer.IsVaulting)
            {
                left = -64f * PoseWeight(progress, .10f, .34f, .53f, .82f);
                right = -58f * PoseWeight(progress, .18f, .43f, .62f, .94f);
                drop = .26f * PoseWeight(progress, 0f, .27f, .61f, 1f);
            }
            else if (explorer.IsGliding)
            {
                float slide = PoseWeight(progress, 0f, .32f, .86f, 1f);
                left = -73f * slide;
                right = -53f * slide;
                drop = .75f * slide;
            }
            if (leftLeg != null) leftLeg.localRotation = leftRest * Quaternion.Euler(left, 0, 0);
            if (rightLeg != null) rightLeg.localRotation = rightRest * Quaternion.Euler(right, 0, 0);
            transform.localPosition = Vector3.Lerp(transform.localPosition, rest + Vector3.down * drop, Time.deltaTime * 12);
        }

        private static float PoseWeight(float progress, float start, float full, float release, float end)
        {
            return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(start, full, progress)) *
                (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(release, end, progress)));
        }
    }
}

using UnityEngine;

namespace DayOfDream.Horror
{
    public sealed class WardrobeDoors : MonoBehaviour
    {
        [SerializeField] private Transform leftHinge, rightHinge;
        internal float Opening { get; private set; }

        public void Configure(Transform left, Transform right)
        {
            leftHinge = left;
            rightHinge = right;
        }

        internal void SetOpening(float degrees)
        {
            Opening = degrees;
            leftHinge.localRotation = Quaternion.Euler(0, degrees, 0);
            rightHinge.localRotation = Quaternion.Euler(0, -degrees, 0);
        }
    }
}

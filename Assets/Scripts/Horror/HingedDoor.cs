using UnityEngine;

namespace DayOfDream.Horror
{
    [RequireComponent(typeof(Rigidbody))]
    public sealed class HingedDoor : MonoBehaviour
    {
        [SerializeField] private float openAngle = 100;
        [SerializeField] private HingedDoor pairedLeaf;
        private Rigidbody hinge;
        private Quaternion closedRotation;
        private float swingAngle;
        [SerializeField, Min(.01f)] private float leafWidth = 1.64f;
        public bool IsOpen { get; private set; }
        public void Configure(float angle, HingedDoor other) { openAngle = angle; pairedLeaf = other; }
        private void Awake() { hinge = GetComponent<Rigidbody>(); closedRotation = hinge.rotation; swingAngle = openAngle; }
        public void Toggle()
        {
            bool next = !IsOpen;
            IsOpen = next;
            if (pairedLeaf != null) pairedLeaf.IsOpen = next;
        }
        public void Toggle(Vector3 actor)
        {
            bool opening = !IsOpen;
            if (opening) ChooseSwing(actor);
            IsOpen = opening;
            if (pairedLeaf != null)
            {
                if (opening) pairedLeaf.ChooseSwing(actor);
                pairedLeaf.IsOpen = opening;
            }
        }

        private void ChooseSwing(Vector3 actor)
        {
            Vector3 local = Quaternion.Inverse(closedRotation) * (actor - hinge.position);
            swingAngle = Mathf.Abs(openAngle) * (local.z >= 0 ? 1 : -1);
        }

        internal bool Connects(Bounds room)
        {
            Vector3 center = hinge.position + closedRotation * new Vector3(leafWidth * .5f, 1.1f, 0);
            Vector3 normal = closedRotation * Vector3.forward * .35f;
            return room.Contains(center + normal) != room.Contains(center - normal);
        }

        internal void OpenForHunter(Vector3 actor, Vector3 destination)
        {
            if (IsOpen) return;
            Vector3 local = Quaternion.Inverse(closedRotation) * (actor - hinge.position);
            Vector3 goal = Quaternion.Inverse(closedRotation) * (destination - hinge.position);
            if (local.z * goal.z >= 0 || Mathf.Abs(local.z) > 1.7f || local.x < -.4f || local.x > leafWidth + .46f) return;
            Toggle(actor);
        }
        private void FixedUpdate()
        {
            Quaternion target = closedRotation * Quaternion.Euler(0, IsOpen ? swingAngle : 0, 0);
            hinge.MoveRotation(Quaternion.RotateTowards(hinge.rotation, target, 135 * Time.fixedDeltaTime));
        }
    }
}

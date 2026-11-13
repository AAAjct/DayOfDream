using UnityEngine;

namespace DayOfDream.Horror
{
    public sealed class HouseObject : MonoBehaviour
    {
        public enum Kind { CabinetKey, ExitKey, Fuse, Cabinet, PowerBox, Exit, Hide, Bottle, HideUnderBed }
        [SerializeField] private Kind kind;
        [SerializeField] private GameObject reveal;
        [SerializeField] private Transform inside, outside;
        internal Kind Type => kind;
        internal GameObject Reveal => reveal;
        internal Transform Inside => inside;
        internal Transform Outside => outside;
        internal bool IsHeld { get; private set; }
        private Transform worldParent;
        private Vector3 worldScale;
        private Quaternion worldRotation;
        private Transform[] parts;
        private int[] layers;
        private Collider[] colliders;
        private bool[] colliderEnabled, colliderTriggers;
        private Rigidbody body;
        private AnonHouse house;
        private FirstPersonExplorer player;
        private AudioSource speaker;
        private AudioClip impactSound;
        private bool initialized, airborne, paused;
        private Vector3 savedVelocity, savedSpin;
        private float nextImpact;
        private string saveId;
        internal string SaveId
        {
            get
            {
                if (saveId != null) return saveId;
                saveId = name;
                for (var parent = transform.parent; parent != null; parent = parent.parent) saveId = parent.name + "/" + saveId;
                return saveId;
            }
        }

        private void Cache()
        {
            if (initialized) return;
            _ = SaveId;
            initialized = true;
            worldParent = transform.parent; worldScale = transform.localScale; worldRotation = transform.rotation;
            parts = GetComponentsInChildren<Transform>(true);
            layers = new int[parts.Length];
            for (int i = 0; i < parts.Length; i++) layers[i] = parts[i].gameObject.layer;
            colliders = GetComponentsInChildren<Collider>(true);
            colliderEnabled = new bool[colliders.Length];
            colliderTriggers = new bool[colliders.Length];
            for (int i = 0; i < colliders.Length; i++)
            { colliderEnabled[i] = colliders[i].enabled; colliderTriggers[i] = colliders[i].isTrigger; }
        }

        internal void Stow()
        {
            Cache();
            IsHeld = false; airborne = false; paused = false;
            if (body != null) body.isKinematic = true;
            transform.SetParent(worldParent, false);
            transform.localScale = worldScale;
            transform.rotation = worldRotation;
            RestoreColliders();
            gameObject.SetActive(false);
        }

        internal void Hold(Transform grip)
        {
            Stow();
            IsHeld = true;
            transform.SetParent(grip, false);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            for (int i = 0; i < parts.Length; i++) parts[i].gameObject.layer = 2;
            foreach (var c in colliders) c.enabled = false;
            gameObject.SetActive(true);
        }

        private void RestoreColliders()
        {
            for (int i = 0; i < parts.Length; i++) parts[i].gameObject.layer = layers[i];
            for (int i = 0; i < colliders.Length; i++)
            { colliders[i].enabled = colliderEnabled[i]; colliders[i].isTrigger = colliderTriggers[i]; }
        }

        internal void Launch(Vector3 position, Vector3 velocity, AnonHouse owner)
        {
            Stow();
            transform.SetParent(null, true);
            transform.position = position;
            house = owner;
            player = owner.GetComponentInChildren<FirstPersonExplorer>();
            if (player == null) player = FindAnyObjectByType<FirstPersonExplorer>();
            if (body == null)
            {
                body = gameObject.AddComponent<Rigidbody>();
                body.mass = kind == Kind.Bottle ? .45f : .15f;
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                body.interpolation = RigidbodyInterpolation.Interpolate;
                body.linearDamping = 0;
                speaker = gameObject.AddComponent<AudioSource>();
                speaker.playOnAwake = false; speaker.spatialBlend = 1; speaker.maxDistance = 22;
                impactSound = Resources.Load<AudioClip>(kind == Kind.Bottle ? "GameplayAudio/BottleBreak" : "GameplayAudio/ItemImpact");
            }
            foreach (var c in colliders) c.isTrigger = false;
            gameObject.SetActive(true);
            var controller = player.GetComponent<CharacterController>();
            foreach (var c in colliders)
                if (c.enabled && c.gameObject.activeInHierarchy) Physics.IgnoreCollision(c, controller);
            body.isKinematic = false;
            body.linearVelocity = velocity;
            body.angularVelocity = new Vector3(3, 6, 2);
            airborne = true;
            nextImpact = Time.time + .08f;
        }

        private void Update()
        {
            if (!airborne || body == null || player == null) return;
            if (player.IsPaused != paused)
            {
                paused = player.IsPaused;
                if (paused)
                { savedVelocity = body.linearVelocity; savedSpin = body.angularVelocity; body.isKinematic = true; speaker.Pause(); }
                else
                { body.isKinematic = false; body.linearVelocity = savedVelocity; body.angularVelocity = savedSpin; speaker.UnPause(); }
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (!airborne || paused || Time.time < nextImpact || collision.relativeVelocity.sqrMagnitude < .35f) return;
            nextImpact = Time.time + .2f;
            if (impactSound != null) speaker.PlayOneShot(impactSound, Mathf.Clamp01(collision.relativeVelocity.magnitude / 4));
            house.Hear(transform.position, kind == Kind.Bottle ? 18 : 8);
        }

        internal void PlaceOnSurface(Vector3 point)
        {
            Stow();
            transform.position = point;
            gameObject.SetActive(true);
            float bottom = point.y;
            foreach (var renderer in GetComponentsInChildren<Renderer>())
                bottom = Mathf.Min(bottom, renderer.bounds.min.y);
            transform.position += Vector3.up * (point.y - bottom + .009f);
        }

        public void Configure(Kind type, GameObject contents = null, Transform hide = null, Transform leave = null)
        {
            kind = type;
            reveal = contents;
            inside = hide;
            outside = leave;
        }
    }
}

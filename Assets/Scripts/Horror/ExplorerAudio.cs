using UnityEngine;

namespace DayOfDream.Horror
{
    public sealed class ExplorerAudio : MonoBehaviour
    {
        private FirstPersonExplorer player;
        private AudioSource steps, effects, execution;
        private AudioClip walk, run, pickup, toss;
        private float distance;
        internal int FootstepsPlayed { get; private set; }

        internal void Initialize(FirstPersonExplorer owner)
        {
            player = owner;
            steps = gameObject.AddComponent<AudioSource>();
            effects = gameObject.AddComponent<AudioSource>();
            steps.playOnAwake = effects.playOnAwake = false;
            walk = Resources.Load<AudioClip>("GameplayAudio/Walk");
            run = Resources.Load<AudioClip>("GameplayAudio/Run");
            pickup = Resources.Load<AudioClip>("GameplayAudio/Pickup");
            toss = Resources.Load<AudioClip>("GameplayAudio/Throw");
            execution = gameObject.AddComponent<AudioSource>();
            execution.playOnAwake = false;
            execution.clip = Resources.Load<AudioClip>("GameplayAudio/ExecutionLaugh");
            execution.volume = .85f;
            execution.loop = true;
        }

        internal void Tick(bool moving)
        {
            if (!moving || player.MovementSpeed < .25f) { distance = 0; return; }
            if (!player.IsGrounded) return;
            distance += player.MovementSpeed * Time.deltaTime;
            float stride = player.IsCrouched ? 1f : player.MovementSpeed > 3.3f ? 1.45f : 1.25f;
            if (distance < stride) return;
            distance %= stride;
            steps.pitch = Random.Range(.93f, 1.07f);
            var clip = player.MovementSpeed > 3.3f ? run : walk;
            // Local feedback is independent of AnonHouse.Hear: walking does not alert Anon.
            if (clip != null)
            { steps.PlayOneShot(clip, player.IsCrouched ? .12f : player.MovementSpeed > 3.3f ? .25f : .35f); FootstepsPlayed++; }
        }

        internal void PickUp() { if (pickup != null) effects.PlayOneShot(pickup, .45f); }
        internal void Throw() { if (toss != null) effects.PlayOneShot(toss, .6f); }
        internal void Execute() { steps.Stop(); if (execution.clip != null) execution.Play(); }
        internal void StopExecution() { if (execution != null) execution.Stop(); }
    }
}

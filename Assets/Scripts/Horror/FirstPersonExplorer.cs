using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DayOfDream.Horror
{
    [RequireComponent(typeof(CharacterController))]
    public sealed partial class FirstPersonExplorer : MonoBehaviour
    {
        private const float DoorImpactDuration = .22f;
        private const float Gravity = 20f;
        private const float JumpHeight = .42f;

        [SerializeField] private InputActionAsset input;

        [SerializeField] private Transform view;

        [SerializeField] private HorrorHud hud;

        [SerializeField] private HandheldFlashlight flashlight;

        [SerializeField] private HandheldCamcorder camcorder;

        [SerializeField] private AnonHouse house;

        [SerializeField] private float walkSpeed = 2.4f;

        [SerializeField] private float runSpeed = 4.5f;

        [SerializeField] private float crouchSpeed = 1.25f;

        [SerializeField] private float mouseSensitivity = 0.095f;

        private CharacterController motor;

        private ExplorerAudio audioFeedback;

        private LeftHandInventory inventory;

        private InputActionAsset actions;

        private InputAction move;

        private InputAction look;

        private InputAction sprint;
        private InputAction jump;
        private Keyboard gameplayKeyboard;
        private bool restoreKeyboardIme;

        private InputAction crouch;

        private InputAction lookBack;

        private InputAction interact;

        private InputAction toggleLight;

        private InputAction pause;
        private float timeScaleBeforePause;
        private bool audioWasPaused;
        private int resumedFrame = -1;

        private InputAction raiseCamera;

        private InputAction toggleInfrared;

        private InputAction cameraScroll;

        private InputAction cameraZoomIn;

        private InputAction cameraZoomOut;

        private float pitch;

        private float fallSpeed;

        private float smoothedEyeY;

        private float eyeVelocity;

        private float rearYaw;

        private float rearVelocity;

        private float stridePhase;

        private float runBlend;
        private float doorImpactUntil;

        private PlayerConsciousness consciousness;

        private Transform wakePillow;

        private Transform wakeExit;

        private float wakeTime;

        private Vector3 executionEye;
        private Vector3 executionPosition;

        private Quaternion executionLook;

        private Vector3 spawn;

        private Quaternion spawnRotation;

        private bool grounded;

        private bool crouched;

        private bool crouchToggled;

        private Vector3 hidingMoveStart;

        private Quaternion hidingLookStart;

        private readonly Collider[] ceilingHits = new Collider[8];

        internal bool IsWaking { get; private set; }

        public float MovementSpeed { get; private set; }

        public bool IsCrouched => crouched;

        public bool IsGrounded => grounded;

        public bool IsPaused { get; private set; }

        public void Configure(InputActionAsset source, Transform cameraView, HorrorHud display, HandheldFlashlight lamp)
        {
            input = source;
            view = cameraView;
            hud = display;
            flashlight = lamp;
        }

        private void Awake()
        {
            AudioListener.volume = DreamSettings.Volume;
            motor = GetComponent<CharacterController>();
            spawn = transform.position;
            spawnRotation = transform.rotation;
            smoothedEyeY = transform.position.y + 1.6f;
            if (input == null || view == null || hud == null)
            {
                Debug.LogError("Explorer requires input, camera and HUD references.", this);
                enabled = false;
                return;
            }
            consciousness = view.gameObject.AddComponent<PlayerConsciousness>();
            consciousness.Initialize(hud);
            actions = Instantiate(input);
            InputActionMap map = actions.FindActionMap("Player", true);
            move = map.FindAction("Move", true);
            look = map.FindAction("Look", true);
            sprint = map.FindAction("Sprint", true);
            jump = map.AddAction("Jump", InputActionType.Button, "<Keyboard>/space");
            crouch = map.FindAction("Crouch", true);
            interact = map.FindAction("Interact", true);
            lookBack = map.FindAction("Look Back", true);
            if (flashlight != null)
            {
                toggleLight = new InputAction("Flashlight", InputActionType.Button, "<Keyboard>/l");
                toggleLight.AddBinding("<Gamepad>/dpad/up");
            }
            if (camcorder != null)
            {
                raiseCamera = map.AddAction("Camcorder", InputActionType.Button, "<Mouse>/rightButton");
                raiseCamera.AddBinding("<Gamepad>/rightShoulder");
                toggleInfrared = map.AddAction("Infrared", InputActionType.Button, "<Keyboard>/f");
                toggleInfrared.AddBinding("<Gamepad>/rightStickPress");
                cameraScroll = map.AddAction("Camcorder Scroll", InputActionType.PassThrough, "<Mouse>/scroll/y");
                cameraZoomIn = map.FindAction("Next", true);
                cameraZoomOut = map.FindAction("Previous", true);
            }
            audioFeedback = gameObject.AddComponent<ExplorerAudio>();
            audioFeedback.Initialize(this);
            inventory = gameObject.AddComponent<LeftHandInventory>();
            inventory.Initialize(house, view, hud, audioFeedback, map);
            foreach (var zoomAction in new[] { cameraZoomIn, cameraZoomOut })
            {
                if (zoomAction == null)
                {
                    continue;
                }
                for (int i = zoomAction.bindings.Count - 1; i >= 0; i--)
                {
                    if (zoomAction.bindings[i].path.Contains("Keyboard"))
                    {
                        zoomAction.ChangeBinding(i).Erase();
                    }
                }
            }
            if (gameObject.scene.name != DreamMainMenu.SceneName)
            {
                hud.BindPauseMenu(this);
                pause = new InputAction("Pause", InputActionType.Button, "<Keyboard>/escape");
                pause.AddBinding("<Gamepad>/start");
            }
        }

        private void OnEnable()
        {
            if (actions != null)
            {
                actions.FindActionMap("Player").Enable();
                toggleLight?.Enable();
                pause?.Enable();
                SetPaused(false);
            }
        }

        private void OnDisable()
        {
            if (IsPaused) SetPaused(false);
            ReleaseGameplayKeyboard();
            inventory?.Tick(false);
            audioFeedback?.StopExecution();
            actions?.Disable();
            toggleLight?.Disable();
            pause?.Disable();
            if (raiseCamera != null)
            {
                camcorder.ResetPresentation();
                if (flashlight != null)
                {
                    flashlight.SetStowed(false);
                }
                hud.SetCamcorderRaised(false);
            }
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void OnDestroy()
        {
            if (actions != null)
            {
                Destroy(actions);
            }
            toggleLight?.Dispose();
            pause?.Dispose();
        }

        private void Update()
        {
            hud.SetInventory(house != null && house.HasCabinetKey, house != null && house.HasExitKey, house != null && house.HasFuse, house != null && house.HasBottle);
            if (pause != null && pause.WasPressedThisFrame())
            {
                if (IsPaused)
                {
                    if (!hud.TryClosePauseSettings()) SetPaused(false);
                }
                else if (IsWaking)
                {
                    FinishWake();
                    return;
                }
                else
                {
                    SetPaused(true);
                }
            }
            if (IsPaused)
            {
                MovementSpeed = 0f;
                audioFeedback.Tick(false);
                inventory.Tick(false);
                return;
            }
            if (resumedFrame == Time.frameCount) return;
            if (IsWaking)
            {
                audioFeedback.Tick(false);
                inventory.Tick(false);
                UpdateWake();
                return;
            }
            if (Cursor.lockState != CursorLockMode.Locked)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
            if (Application.isFocused && gameplayKeyboard != Keyboard.current)
                CaptureGameplayKeyboard();
            if (house != null && house.BlocksMovement)
            {
                MovementSpeed = 0f;
                audioFeedback.Tick(false);
                inventory.Tick(false);
                if (house.IsExecuting) return;
                if (!house.AdvanceWardrobeMove(Time.deltaTime))
                {
                    if (house.Hidden)
                    {
                        UpdateHiddenLook(house.IsUnderBed);
                        UpdateEquipment();
                    }
                    if (interact.WasPressedThisFrame())
                    {
                        house.InteractWhileStopped();
                    }
                }
                return;
            }
            Vector2 delta = ReadLook();
            transform.Rotate(0f, delta.x, 0f);
            pitch = Mathf.Clamp(pitch - delta.y, -78f, 78f);
            rearYaw = Mathf.SmoothDamp(rearYaw, lookBack.IsPressed() ? 180 : 0, ref rearVelocity, 0.13f);
            if (crouch.WasPressedThisFrame())
            {
                crouchToggled = !crouchToggled;
            }
            Vector2 axis = Vector2.ClampMagnitude(move.ReadValue<Vector2>(), 1f);
            Vector3 direction = transform.right * axis.x + transform.forward * axis.y;
            bool running = false;
            bool jumpPressed = false;
            if (traversal == Traversal.None)
            {
                bool wasCrouched = crouched;
                if (crouchToggled || CanStand())
                {
                    crouched = crouchToggled;
                }
                if (wasCrouched != crouched)
                {
                    SetControllerHeight(crouched ? CrouchHeight : StandingHeight);
                }

                running = !crouched && sprint.IsPressed();
                jumpPressed = motor.isGrounded && !crouched && jump.WasPressedThisFrame();
                if (jumpPressed && axis.sqrMagnitude > .01f)
                {
                    TryStartTraversal(direction.normalized, running, axis.magnitude);
                }
            }
            if (traversal != Traversal.None)
            {
                UpdateTraversal(direction);
                audioFeedback.Tick(false);
                inventory.Tick(false);
                UpdateEquipment();
                hud.SetPrompt("");
                return;
            }
            float speed = crouched ? crouchSpeed : running ? runSpeed : walkSpeed;
            bool onGround = motor.isGrounded;
            if (onGround && fallSpeed < 0f)
            {
                fallSpeed = -2f;
            }
            if (jumpPressed)
            {
                fallSpeed = Mathf.Sqrt(2f * Gravity * JumpHeight);
            }
            float verticalStep = (fallSpeed - Gravity * Time.deltaTime * .5f) * Time.deltaTime;
            fallSpeed = Mathf.Max(-35f, fallSpeed - Gravity * Time.deltaTime);
            Vector3 before = transform.position;
            CollisionFlags flags = motor.Move(direction * (speed * Time.deltaTime) + Vector3.up * verticalStep);
            if ((flags & CollisionFlags.Above) != 0 && fallSpeed > 0f)
            {
                fallSpeed = 0f;
            }
            grounded = (flags & CollisionFlags.Below) != 0;
            MovementSpeed = new Vector2(transform.position.x - before.x, transform.position.z - before.z).magnitude / Mathf.Max(0.001f, Time.deltaTime);
            audioFeedback.Tick(true);
            UpdateView(running);
            inventory.Tick(true);
            UpdateEquipment();
            Transform aim = ((camcorder != null) ? camcorder.Aim : view);
            bool hasHit = Physics.Raycast(aim.position, aim.forward, out var hit, 2.5f, ~(1 << 2), QueryTriggerInteraction.Collide);
            ReadableNote note = (hasHit ? hit.collider.GetComponentInParent<ReadableNote>() : null);
            HingedDoor door = (hasHit ? hit.collider.GetComponentInParent<HingedDoor>() : null);
            CamcorderPickup cameraPickup = ((hasHit && camcorder != null) ? hit.collider.GetComponentInParent<CamcorderPickup>() : null);
            HouseObject target = ((hasHit && house != null) ? hit.collider.GetComponentInParent<HouseObject>() : null);
            hud.SetPrompt("", target != null || door != null || note != null || cameraPickup != null);
            if (interact.WasPressedThisFrame())
            {
                if (target != null)
                {
                    house.Use(target);
                }
                else if (door != null)
                {
                    Vector3 towardDoor = Vector3.ProjectOnPlane(hit.point - aim.position, Vector3.up);
                    bool forceOpen = !door.IsOpen && running && grounded &&
                        Vector3.Dot(direction, towardDoor.normalized) > .35f;
                    door.Toggle(view.position, forceOpen);
                    if (forceOpen)
                    {
                        doorImpactUntil = Time.time + DoorImpactDuration;
                        ApplyDoorImpact(1f);
                    }
                    if (house != null)
                    {
                        house.Hear(door.transform.position, 5f);
                    }
                }
                else if (note != null)
                {
                    hud.ShowNote(note.Title, note.Body);
                }
                else if (cameraPickup != null)
                {
                    cameraPickup.TryPickUp(camcorder);
                }
            }
            if (transform.position.y < -10f)
            {
                Respawn();
            }
        }

        private Vector2 ReadLook()
        {
            bool pointer = look.activeControl?.device is Pointer;
            return look.ReadValue<Vector2>() * (pointer ? mouseSensitivity * DreamSettings.Sensitivity : (115f * Time.deltaTime));
        }

        private void UpdateHiddenLook(bool underBed)
        {
            Vector2 delta = ReadLook();
            rearYaw = Mathf.Clamp(rearYaw + delta.x, underBed ? (-35) : (-13), underBed ? 35 : 13);
            pitch = Mathf.Clamp(pitch - delta.y, underBed ? (-7) : (-20), underBed ? 7 : 20);
            view.localRotation = Quaternion.Euler(pitch, rearYaw, 0f);
        }

        private void UpdateView(bool running)
        {
            float dt = Time.deltaTime;
            float pace = Mathf.Clamp01(MovementSpeed / runSpeed);
            float target = ((running && grounded) ? Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.35f, 1f, pace)) : 0f);
            runBlend = Mathf.Lerp(runBlend, target, 1f - Mathf.Exp(-10f * dt));
            stridePhase = Mathf.Repeat(stridePhase + MathF.PI * 2f * (1.15f + 0.5f * pace) * dt, MathF.PI * 2f);
            float sway = Mathf.Sin(stridePhase);
            float footfall = Mathf.Cos(stridePhase * 2f);
            smoothedEyeY = Mathf.SmoothDamp(smoothedEyeY, transform.position.y + (crouched ? 0.98f : 1.6f), ref eyeVelocity, 0.085f);
            Quaternion heading = Quaternion.Euler(0f, rearYaw, 0f);
            view.localPosition = Vector3.up * (smoothedEyeY - transform.position.y) + heading * new Vector3(0.019f * sway * runBlend, 0.024f * footfall * runBlend, 0.055f);
            view.localRotation = heading * Quaternion.Euler(pitch + 0.85f * footfall * runBlend, 0.55f * sway * runBlend, 1.15f * sway * runBlend);
            if (doorImpactUntil > Time.time)
            {
                float impact = (doorImpactUntil - Time.time) / DoorImpactDuration;
                ApplyDoorImpact(impact * impact);
            }
        }

        private void ApplyDoorImpact(float strength)
        {
            view.localPosition -= Quaternion.Euler(0f, rearYaw, 0f) * Vector3.forward * (.045f * strength);
            view.localRotation *= Quaternion.Euler(2f * strength, 0f, 0f);
        }

        private void UpdateEquipment()
        {
            bool lightPressed = toggleLight != null && toggleLight.WasPressedThisFrame();
            if (camcorder != null && camcorder.IsCarried)
            {
                if (raiseCamera.WasPressedThisFrame())
                {
                    camcorder.ToggleRaised();
                }
                if (toggleInfrared.WasPressedThisFrame())
                {
                    camcorder.ToggleNightVision();
                }
                float scroll = cameraScroll.ReadValue<float>();
                if (scroll != 0) camcorder.AdjustZoom(Mathf.Sign(scroll));
                if (cameraZoomIn.WasPressedThisFrame()) camcorder.AdjustZoom(1);
                if (cameraZoomOut.WasPressedThisFrame()) camcorder.AdjustZoom(-1);
                if (lightPressed && camcorder.IsRaised)
                {
                    camcorder.Lower();
                    flashlight.SetOn(true);
                }
                else if (lightPressed) flashlight.Toggle();
                camcorder.Tick(Time.deltaTime, stridePhase, runBlend, IsCrouched);
                if (flashlight != null) flashlight.SetStowed(camcorder.OccupiesHands);
                hud.SetCamcorderRaised(camcorder.EffectWeight > .5f);
            }
            else if (lightPressed) flashlight.Toggle();
        }

        private bool CanStand()
        {
            if (!crouched) return true;
            return HasCapsuleClearance(transform.position + Vector3.up * 1.05f,
                transform.position + Vector3.up * 1.51f, .27f);
        }

        private bool HasCapsuleClearance(Vector3 lower, Vector3 upper, float radius)
        {
            int count = Physics.OverlapCapsuleNonAlloc(lower, upper, radius, ceilingHits,
                ObstacleMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++) if (ceilingHits[i] != motor) return false;
            return count < ceilingHits.Length;
        }

        public void SetPaused(bool value)
        {
            if (value && (pause == null || !isActiveAndEnabled)) return;
            if (value && !IsPaused)
            {
                house?.SaveProgress();
                timeScaleBeforePause = Time.timeScale;
                audioWasPaused = AudioListener.pause;
                Time.timeScale = 0f;
                AudioListener.pause = true;
                inventory?.Cancel();
            }
            else if (!value && IsPaused)
            {
                Time.timeScale = timeScaleBeforePause;
                AudioListener.pause = audioWasPaused;
                resumedFrame = Time.frameCount;
            }
            IsPaused = value;
            if (value) ReleaseGameplayKeyboard();
            else if (Application.isFocused && isActiveAndEnabled) CaptureGameplayKeyboard();
            Cursor.lockState = value ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = value;
            if (hud != null) hud.SetPaused(value);
        }

        public void Respawn()
        {
            camcorder?.Lower();
            Warp(spawn, spawnRotation);
        }

        internal void PrepareForExecution(bool extracting)
        {
            traversal = Traversal.None;
            inventory?.Tick(false);
            if (!extracting) PlayExecutionAudio();
            executionEye = view.position;
            executionPosition = transform.position;
            executionLook = view.rotation;
            consciousness.Set(0, 1);
            motor.enabled = false;
            MovementSpeed = 0;
            hud.SetCinematic(true);
            camcorder?.DropAtDeath(transform.position);
            camcorder?.SetCinematic(true);
            if (flashlight != null) flashlight.SetStowed(false);
            hud.SetCamcorderRaised(false);
        }

        internal void PlayExecutionAudio() => audioFeedback?.Execute();
        internal void StopExecutionAudio() => audioFeedback?.StopExecution();

        internal void FrameExtraction(Vector3 attacker, HouseObject spot, float time, float duration)
        {
            bool bed = spot.Type == HouseObject.Kind.HideUnderBed;
            Vector3 exit = spot.Outside.position;
            Vector3 facing = Vector3.ProjectOnPlane(attacker - exit, Vector3.up);
            Quaternion towardHunter = facing.sqrMagnitude > .001f ? Quaternion.LookRotation(facing) : spot.Outside.rotation;
            float grab = Smooth(time, bed ? .15f : .34f, bed ? .54f : .73f);
            float pull = Smooth(time, bed ? .53f : .72f, duration);
            transform.SetPositionAndRotation(Vector3.Lerp(executionPosition, exit, pull), towardHunter);
            Vector3 extracted = exit + Vector3.up * (bed ? .29f : .85f);
            Vector3 eye = Vector3.Lerp(executionEye, extracted, pull);
            eye.y += (bed ? .045f : -.16f) * Mathf.Sin(pull * Mathf.PI);
            Vector3 lookTarget = attacker + Vector3.up * (bed ? Mathf.Lerp(.82f, 1.38f, pull) : 1.42f);
            Quaternion pulledLook = Quaternion.LookRotation(lookTarget - eye) * Quaternion.Euler(0, 0, bed ? -14 : 13);
            view.SetPositionAndRotation(eye, Quaternion.Slerp(executionLook, pulledLook, grab));
            if (time < duration) return;
            executionEye = view.position;
            executionLook = view.rotation;
        }

        internal void FrameExecution(Vector3 attacker, float time)
        {
            float fall = Smooth(time, 0, .62f);
            Vector3 flat = Vector3.ProjectOnPlane(attacker - transform.position, Vector3.up);
            if (flat.sqrMagnitude > .001f) transform.rotation = Quaternion.LookRotation(flat);
            Vector3 fallen = transform.position + Vector3.up * .29f - transform.forward * .12f;
            view.position = Vector3.Lerp(executionEye, fallen, fall);
            Vector3 target = attacker + Vector3.up * 1.40f;
            Quaternion upward = Quaternion.LookRotation(target - view.position) * Quaternion.Euler(0, 0, -13);
            view.rotation = Quaternion.Slerp(executionLook, upward, fall);
            float impact = Mathf.InverseLerp(1.95f, 2.15f, time);
            view.position += view.right * (Mathf.Sin(time * 90) * .025f * (1 - impact) *
                (time >= 1.95f ? 1 : 0));
            consciousness.Set(impact, 1 - impact);
        }

        internal void BeginWake(Transform pillow, Transform exit)
        {
            wakePillow = pillow;
            wakeExit = exit;
            IsWaking = true;
            wakeTime = 0;
            camcorder?.SetCinematic(true);
            Warp(exit.position, exit.rotation);
            crouched = crouchToggled = false;
            motor.height = 1.8f;
            motor.center = Vector3.up * .9f;
            motor.enabled = false;
            MovementSpeed = 0;
            flashlight?.SetStowed(true);
            hud.SetCinematic(true);
            hud.DismissNote();
            PoseWake();
        }

        private void UpdateWake()
        {
            wakeTime += Time.deltaTime;
            if (wakeTime >= 7.2f) FinishWake();
            else PoseWake();
        }

        private void FinishWake()
        {
            IsWaking = false;
            camcorder?.SetCinematic(false);
            Warp(wakeExit.position, wakeExit.rotation);
            consciousness.Set(0, 1);
            flashlight?.SetStowed(false);
            hud.SetCinematic(false);
        }

        private void PoseWake()
        {
            float sit = Smooth(wakeTime, 3.05f, 4.65f);
            float turn = Smooth(wakeTime, 4.25f, 5.55f);
            float stand = Smooth(wakeTime, 5.4f, 7.2f);
            Vector3 lying = wakePillow.position;
            Vector3 seated = lying + wakePillow.forward * .45f + Vector3.up * .53f;
            Vector3 bedside = wakeExit.position + Vector3.up * 1.03f;
            Vector3 standing = wakeExit.position + Vector3.up * 1.60f + wakeExit.forward * .055f;
            view.position = Vector3.Lerp(Vector3.Lerp(Vector3.Lerp(lying, seated, sit), bedside, turn), standing, stand);
            Quaternion lyingLook = wakePillow.rotation * Quaternion.Euler(-79, 0, -6);
            Quaternion seatedLook = wakePillow.rotation * Quaternion.Euler(22, 0, 0);
            Quaternion exitLook = wakeExit.rotation * Quaternion.Euler(26 * (1 - stand), 0, 0);
            view.rotation = Quaternion.Slerp(Quaternion.Slerp(lyingLook, seatedLook, sit), exitLook, turn);
            view.rotation *= Quaternion.Euler(Mathf.Sin(wakeTime * 2) * .45f, 0,
                Mathf.Sin(wakeTime * 1.7f) * .55f * (1 - stand));
            float aperture = Smooth(wakeTime, .55f, 1.8f);
            aperture *= 1 - .88f * (Smooth(wakeTime, 1.95f, 2.12f) - Smooth(wakeTime, 2.23f, 2.65f));
            consciousness.Set(1 - Smooth(wakeTime, 2.5f, 6.5f), aperture);
        }

        private static float Smooth(float value, float start, float end) =>
            Mathf.SmoothStep(0, 1, Mathf.InverseLerp(start, end, value));

        internal void HideUnderBed(Transform spot)
        {
            // The controller is taller than the gap under the bed.
            Warp(spot.position, spot.rotation, .11f);
        }

        internal void BeginWardrobeMove()
        {
            motor.enabled = false;
            hidingMoveStart = view.position;
            hidingLookStart = view.rotation;
        }

        internal void PoseWardrobeMove(Transform inside, Transform outside, bool entering, float travel)
        {
            Transform destination = entering ? inside : outside;
            Vector3 end = destination.position + destination.rotation * new Vector3(0,
                entering ? 1.44f : crouched ? .98f : 1.60f, .055f);
            Vector3 position = Vector3.Lerp(hidingMoveStart, end, travel);
            position.y -= .18f * Mathf.Sin(Mathf.PI * travel);
            Quaternion rotation = Quaternion.Slerp(hidingLookStart, destination.rotation, travel);
            view.SetPositionAndRotation(position, rotation);
            UpdateEquipment();
        }

        internal void FinishWardrobeMove(Transform destination, bool entering)
        {
            Warp(destination.position, destination.rotation, entering ? 1.44f : null);
        }

        internal void Warp(Vector3 position, Quaternion rotation, float? hidingEyeHeight = null)
        {
            traversal = Traversal.None;
            doorImpactUntil = 0f;
            motor.enabled = false;
            transform.SetPositionAndRotation(position, rotation);
            motor.enabled = !hidingEyeHeight.HasValue;
            pitch = fallSpeed = eyeVelocity = rearYaw = rearVelocity = stridePhase = runBlend = 0;
            smoothedEyeY = position.y + (hidingEyeHeight ?? (crouched ? .98f : 1.60f));
            view.localRotation = Quaternion.identity;
            view.localPosition = new Vector3(0, smoothedEyeY - position.y, .055f);
        }

        private void CaptureGameplayKeyboard()
        {
            var keyboard = Keyboard.current;
            if (keyboard == gameplayKeyboard) return;
            ReleaseGameplayKeyboard();
            if (keyboard == null) return;
            restoreKeyboardIme = keyboard.imeSelected.isPressed;
            gameplayKeyboard = keyboard;
            keyboard.SetIMEEnabled(false);
        }

        private void ReleaseGameplayKeyboard()
        {
            if (gameplayKeyboard != null && gameplayKeyboard.added && restoreKeyboardIme)
                gameplayKeyboard.SetIMEEnabled(true);
            gameplayKeyboard = null;
            restoreKeyboardIme = false;
        }

        private void OnApplicationFocus(bool focus)
        {
            if (!focus && hud != null) SetPaused(true);
            else if (focus && !IsPaused && isActiveAndEnabled) CaptureGameplayKeyboard();
        }
    }
}

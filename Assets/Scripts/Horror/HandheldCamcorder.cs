using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace DayOfDream.Horror
{
    [DisallowMultipleComponent, RequireComponent(typeof(Camera))]
    public sealed class HandheldCamcorder : MonoBehaviour
    {
        [SerializeField] private Light infrared;
        [SerializeField] private Transform model;
        [SerializeField] private CamcorderViewfinder viewfinder;
        [SerializeField] private AudioClip switchSound;
        [SerializeField] private AudioSource speaker;
        [SerializeField] private CamcorderPickup pickup;

        private const float MaxZoom = 3f;
        private const int ViewModelLayer = 31;
        private static readonly Vector3 EyePosition = new(.106f, -.002f, .148f);
        private static readonly Vector3 LoweredPosition = new(.205f, -.174f, .385f);
        private static readonly Vector3 LiftControl = new(.252f, -.040f, .358f);
        private static readonly Quaternion LensOrientation = Quaternion.Euler(0, 180, 0);
        private static readonly int BaseMap = Shader.PropertyToID("_BaseMap");
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionMap = Shader.PropertyToID("_EmissionMap");
        private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
        private Camera eye;
        private UniversalAdditionalCameraData eyeData;
        private int eyeCullingMask;
        private bool eyeShadows, eyePostProcessing, eyeCovered;
        private Camera lens;
        private RenderTexture feed;
        private Renderer lcd;
        private Mesh lcdMesh;
        private MaterialPropertyBlock lcdProperties;
        private float fullInfraredIntensity;
        private float raisedAmount, recordedTime, zoom, targetZoom;
        private float infraredBlend;
        private float nextPreviewFrame;
        private bool fullSizeFeed;
        private float infraredGain = 1, targetInfraredGain = 1, nextMeter;
        private bool nightVision, stowedForCinematic;

        public bool IsRaised { get; private set; }
        public bool IsCarried { get; private set; }
        public bool InfraredActive => IsCarried && nightVision && (IsRaised || raisedAmount > .25f);
        internal bool OccupiesHands => IsCarried && (IsRaised || raisedAmount > .12f);
        internal float EffectWeight => Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.73f, .97f, raisedAmount));
        internal float InfraredWeight => infraredBlend;
        internal float InfraredRange => infrared.range;
        internal Transform Aim => EffectWeight > .75f ? lens.transform : eye.transform;

        private void Awake()
        {
            eye = GetComponent<Camera>();
            eyeData = eye.GetUniversalAdditionalCameraData();
            fullInfraredIntensity = infrared.intensity;
            infrared.range = 10;
            infrared.spotAngle = 83;
            infrared.innerSpotAngle = 32;
            infrared.shadows = LightShadows.Soft;
            infrared.shadowBias = .025f;
            infrared.shadowNormalBias = .08f;
            IsCarried = pickup == null;
            CreateLens();
            CreateLcd();
            foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
            {
                renderer.gameObject.layer = ViewModelLayer;
            }
            ResetPresentation();
        }

        private void OnEnable()
        {
            nextPreviewFrame = 0;
            RenderPipelineManager.beginCameraRendering += BeginCamera;
            RenderPipelineManager.endCameraRendering += EndCamera;
        }

        private void CreateLens()
        {
            var cameraObject = new GameObject("DV Lens", typeof(Camera), typeof(CamcorderSensorCamera));
            cameraObject.transform.SetParent(eye.transform, false);
            lens = cameraObject.GetComponent<Camera>();
            lens.CopyFrom(eye);
            lens.enabled = false;
            lens.cullingMask &= ~(1 << ViewModelLayer);
            lens.depth = eye.depth - 10;
            lens.nearClipPlane = .09f;
            lens.farClipPlane = Mathf.Min(eye.farClipPlane, 110);
            lens.allowMSAA = false;
            lens.allowDynamicResolution = false;
            var lensData = lens.GetUniversalAdditionalCameraData();
            lensData.renderPostProcessing = true;
            lensData.antialiasing = Application.isMobilePlatform
                ? AntialiasingMode.FastApproximateAntialiasing
                : AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            lensData.antialiasingQuality = AntialiasingQuality.High;
            cameraObject.GetComponent<CamcorderSensorCamera>().Owner = this;
        }

        private void CreateLcd()
        {
            var display = model.Find("Display");
            if (display == null || !display.TryGetComponent(out Renderer glass)) return;
            lcdProperties = new MaterialPropertyBlock();

            // Use a single screen surface. The imported Display locates it and supplies its material.
            glass.enabled = false;
            var panel = new GameObject("Live LCD", typeof(MeshFilter), typeof(MeshRenderer));
            panel.transform.SetParent(model, false);
            panel.transform.localPosition = display.localPosition;
            lcdMesh = new Mesh { name = "DV LCD Surface" };
            lcdMesh.vertices = new[]
            {
                new Vector3(.0405f, -.0225f, 0), new Vector3(-.0405f, -.0225f, 0),
                new Vector3(-.0405f, .0225f, 0), new Vector3(.0405f, .0225f, 0)
            };
            lcdMesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up };
            lcdMesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            lcdMesh.RecalculateBounds();
            lcdMesh.RecalculateNormals();
            panel.GetComponent<MeshFilter>().sharedMesh = lcdMesh;
            lcd = panel.GetComponent<MeshRenderer>();
            lcd.sharedMaterial = glass.sharedMaterial;
            lcd.shadowCastingMode = ShadowCastingMode.Off;
            lcd.receiveShadows = false;
            lcd.renderingLayerMask = glass.renderingLayerMask;
        }

        private void EnsureFeed()
        {
            float aspect = Mathf.Clamp(eye.pixelWidth / (float)Mathf.Max(1, eye.pixelHeight), .5f, 4f);
            fullSizeFeed = IsRaised || (fullSizeFeed && raisedAmount > .12f);
            int height = Application.isMobilePlatform
                ? (fullSizeFeed ? Mathf.Clamp(eye.pixelHeight, 360, 600) : 180)
                : (fullSizeFeed ? Mathf.Clamp(eye.pixelHeight, 540, 900) : 270);
            int width = Mathf.RoundToInt(height * aspect);
            if (feed != null && feed.width == width && feed.height == height) return;
            if (feed != null) Destroy(feed);
            feed = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
            {
                name = "DV Live Feed",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            lens.targetTexture = feed;
            lens.aspect = width / (float)height;
            viewfinder.SetFeed(feed);
            if (lcd != null)
            {
                lcdProperties.SetTexture(BaseMap, Texture2D.whiteTexture);
                lcdProperties.SetColor(BaseColor, Color.black);
                lcdProperties.SetTexture(EmissionMap, feed);
                lcdProperties.SetColor(EmissionColor, new Color(.75f, .84f, .79f));
                lcd.SetPropertyBlock(lcdProperties);
            }
        }

        public void ToggleRaised()
        {
            if (!IsCarried) return;
            if (IsRaised) Lower();
            else
            {
                IsRaised = true;
                Play(switchSound);
            }
        }

        public void Lower()
        {
            if (!IsRaised) return;
            IsRaised = false;
            targetZoom = 0;
            Play(switchSound);
        }

        public void ToggleNightVision()
        {
            if (!IsRaised) return;
            nightVision = !nightVision;
            Play(switchSound);
        }

        internal bool TryEquip(CamcorderPickup source)
        {
            if (IsCarried || source != pickup) return false;
            IsCarried = true;
            ResetPresentation();
            Play(switchSound);
            return true;
        }

        internal void RestoreEquipment(bool carried)
        {
            IsCarried = carried;
            if (pickup != null) pickup.gameObject.SetActive(!carried);
            ResetPresentation();
        }

        internal void DropAtDeath(Vector3 position)
        {
            // The DV is permanent equipment, including across nights.
            ResetPresentation();
        }

        internal void ReturnToTable()
        {
            if (pickup == null || (!IsCarried && pickup.gameObject.activeSelf)) return;
            IsCarried = false;
            ResetPresentation();
            pickup.gameObject.SetActive(true);
        }

        public void AdjustZoom(float step)
        {
            if (IsRaised)
                targetZoom = Mathf.Clamp01(targetZoom + step * .14f);
        }

        public void Tick(float deltaTime, float stride, float runWeight, bool crouched)
        {
            if (!IsCarried) return;
            if (IsRaised) recordedTime += deltaTime;
            raisedAmount = Mathf.MoveTowards(raisedAmount, IsRaised ? 1 : 0,
                deltaTime / (IsRaised ? .48f : .39f));
            zoom = Mathf.MoveTowards(zoom, targetZoom, deltaTime * 2.8f);
            infraredBlend = Mathf.MoveTowards(infraredBlend, InfraredActive ? 1 : 0, deltaTime * 4.8f);
            UpdatePresentation(stride, runWeight, crouched);
            MeterInfrared(deltaTime);
        }

        private void UpdatePresentation(float stride, float runWeight, bool crouched)
        {
            EnsureFeed();
            float lift = Mathf.SmoothStep(0, 1, raisedAmount);
            float arc = Mathf.Sin(lift * Mathf.PI);
            float carry = (1 - lift) * runWeight;
            float armSwing = Mathf.Sin(stride - .55f);
            float heel = Mathf.Cos(stride * 2 - .8f);
            float magnification = Mathf.Lerp(1, MaxZoom, zoom * EffectWeight);
            float halfFovTangent = Mathf.Tan(eye.fieldOfView * .5f * Mathf.Deg2Rad);
            lens.fieldOfView = 2 * Mathf.Atan(halfFovTangent / magnification) * Mathf.Rad2Deg;
            float breath = Mathf.Sin(Time.time * 2.1f);
            Vector3 handPath = Vector3.Lerp(Vector3.Lerp(LoweredPosition, LiftControl, lift),
                Vector3.Lerp(LiftControl, EyePosition, lift), lift);
            model.localPosition = handPath +
                new Vector3(.016f * armSwing * carry,
                    (.014f * heel - .009f * armSwing) * carry +
                    (.003f * breath + (crouched ? .023f : 0)) * (1 - lift),
                    .023f * armSwing * carry);
            Quaternion aim = Quaternion.Euler((15f + 4f * heel * runWeight) * (1 - lift) - 7f * arc,
                (-11f + 4f * armSwing * runWeight) * (1 - lift) - 6f * arc,
                (13f + 5f * armSwing * runWeight) * (1 - lift) + 7f * arc);
            model.localRotation = aim * LensOrientation;
            model.gameObject.SetActive(!stowedForCinematic && EffectWeight < .999f);

            Vector3 origin = eye.transform.position;
            Vector3 opticalCenter = model.TransformPoint(new Vector3(0, .002f, -.139f));
            opticalCenter = Vector3.Lerp(opticalCenter, eye.transform.TransformPoint(new Vector3(0, 0, .055f)), EffectWeight);
            float handheldSway = EffectWeight * runWeight;
            opticalCenter += eye.transform.TransformVector(new Vector3(.007f * armSwing, .009f * heel, 0)) * handheldSway;
            Vector3 offset = opticalCenter - origin;
            if (offset.sqrMagnitude > .0001f && Physics.Raycast(origin, offset.normalized, out var wall,
                offset.magnitude, ~(1 << 2), QueryTriggerInteraction.Ignore))
                opticalCenter = origin + offset.normalized * Mathf.Max(0, wall.distance - .035f);
            Quaternion shake = Quaternion.Euler(.55f * heel * handheldSway,
                .4f * armSwing * handheldSway, .65f * armSwing * handheldSway);
            lens.transform.SetPositionAndRotation(opticalCenter, eye.transform.rotation * aim * shake);
            infrared.transform.SetPositionAndRotation(opticalCenter, lens.transform.rotation);
            viewfinder.Present(EffectWeight, InfraredActive, zoom, recordedTime);
        }

        private void MeterInfrared(float deltaTime)
        {
            if (InfraredWeight <= 0)
            {
                infraredGain = targetInfraredGain = 1;
                nextMeter = 0;
                return;
            }
            if (Time.time >= nextMeter)
            {
                nextMeter = Time.time + .12f;
                float distance = Physics.SphereCast(lens.transform.position, .12f, lens.transform.forward, out var surface,
                    5f, ~(1 << 2), QueryTriggerInteraction.Ignore) ? surface.distance : 5f;
                targetInfraredGain = Mathf.Lerp(.055f, 1, Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.24f, 5f, distance)));
            }
            infraredGain = Mathf.Lerp(infraredGain, targetInfraredGain, 1 - Mathf.Exp(-8 * deltaTime));
        }

        private void BeginCamera(ScriptableRenderContext context, Camera camera)
        {
            if (camera != lens || infraredBlend <= .001f) return;
            infrared.intensity = fullInfraredIntensity * infraredBlend * infraredGain;
            infrared.enabled = true;
        }

        private void LateUpdate()
        {
            if (lens == null) return;
            CoverEye(IsCarried && !stowedForCinematic && EffectWeight >= .9999f);
            // The small carry LCD doesn't need a second world render on every display frame.
            float now = Time.unscaledTime;
            lens.enabled = IsCarried && !stowedForCinematic && feed != null && (raisedAmount > .08f || now >= nextPreviewFrame);
            if (lens.enabled) nextPreviewFrame = now + 1f / (Application.isMobilePlatform ? 10 : 18);
        }

        private void CoverEye(bool covered)
        {
            if (covered == eyeCovered) return;
            eyeCovered = covered;
            if (covered)
            {
                // The opaque optical feed covers the entire Game view at this point.
                eyeCullingMask = eye.cullingMask;
                eyeShadows = eyeData.renderShadows;
                eyePostProcessing = eyeData.renderPostProcessing;
                eye.cullingMask = 0;
                eyeData.renderShadows = false;
                eyeData.renderPostProcessing = false;
            }
            else
            {
                eye.cullingMask = eyeCullingMask;
                eyeData.renderShadows = eyeShadows;
                eyeData.renderPostProcessing = eyePostProcessing;
            }
        }

        private void EndCamera(ScriptableRenderContext context, Camera camera)
        {
            if (camera != lens) return;
            infrared.enabled = false;
            lens.enabled = false;
        }

        private void Play(AudioClip clip)
        {
            if (speaker != null && clip != null) speaker.PlayOneShot(clip);
        }

        private void OnDisable()
        {
            CoverEye(false);
            RenderPipelineManager.beginCameraRendering -= BeginCamera;
            RenderPipelineManager.endCameraRendering -= EndCamera;
            if (lens != null) lens.enabled = false;
            if (eye != null) ResetPresentation();
        }

        private void OnDestroy()
        {
            if (lens != null) Destroy(lens.gameObject);
            if (lcd != null) Destroy(lcd.gameObject);
            if (feed != null) Destroy(feed);
            if (lcdMesh != null) Destroy(lcdMesh);
        }

        internal void SetCinematic(bool value)
        {
            stowedForCinematic = value;
            ResetPresentation();
        }

        internal void ResetPresentation()
        {
            IsRaised = false;
            raisedAmount = zoom = targetZoom = 0;
            infraredBlend = 0;
            infraredGain = targetInfraredGain = 1;
            nextMeter = 0;
            nightVision = false;
            infrared.enabled = false;
            if (IsCarried) UpdatePresentation(0, 0, false);
            else
            {
                lens.enabled = false;
                model.gameObject.SetActive(false);
                viewfinder.Present(0, false, 0, recordedTime);
            }
        }
    }
}

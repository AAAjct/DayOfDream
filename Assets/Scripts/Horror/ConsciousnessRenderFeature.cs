using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace DayOfDream.Horror
{
    public sealed class ConsciousnessRenderFeature : ScriptableRendererFeature
    {
        [SerializeField] private Shader shader;
        private Material material;
        private ConsciousnessPass pass;

        public override void Create()
        {
            CoreUtils.Destroy(material);
            if (shader == null) return;
            material = CoreUtils.CreateEngineMaterial(shader);
            pass = new ConsciousnessPass(material)
            {
                renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing,
                requiresIntermediateTexture = true
            };
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            Camera camera = renderingData.cameraData.camera;
            if (material == null || camera.cameraType != CameraType.Game ||
                !camera.TryGetComponent(out PlayerConsciousness sensor) || sensor.Blur <= .001f)
                return;
            renderer.EnqueuePass(pass);
        }

        protected override void Dispose(bool disposing)
        {
            CoreUtils.Destroy(material);
            material = null;
        }

        private sealed class ConsciousnessPass : ScriptableRenderPass
        {
            private static readonly int Sensor = Shader.PropertyToID("_Consciousness");
            private static readonly int Source = Shader.PropertyToID("_BlitTexture");
            private static readonly int ScaleBias = Shader.PropertyToID("_BlitScaleBias");
            private static readonly Vector4 IdentityScaleBias = new(1, 1, 0, 0);
            private readonly Material material;
            private readonly MaterialPropertyBlock properties = new();

            private sealed class PassData
            {
                public TextureHandle source;
                public Material material;
                public MaterialPropertyBlock properties;
                public Vector4 sensor;
            }

            public ConsciousnessPass(Material material) => this.material = material;

            public override void RecordRenderGraph(RenderGraph graph, ContextContainer frameData)
            {
                var resources = frameData.Get<UniversalResourceData>();
                if (resources.isActiveTargetBackBuffer) return;
                var camera = frameData.Get<UniversalCameraData>().camera;
                if (!camera.TryGetComponent(out PlayerConsciousness sensor) || sensor.Blur <= .001f) return;
                

                var source = resources.activeColorTexture;
                var descriptor = graph.GetTextureDesc(source);
                descriptor.name = "Waking Vision";
                descriptor.clearBuffer = false;
                descriptor.msaaSamples = MSAASamples.None;
                descriptor.depthBufferBits = DepthBits.None;
                var output = graph.CreateTexture(descriptor);
                using (var builder = graph.AddRasterRenderPass<PassData>("Waking Vision", out var data))
                {
                    data.source = source;
                    data.material = material;
                    data.properties = properties;
                    data.sensor = new Vector4(sensor.Blur, 0, 0, 0);
                    builder.UseTexture(source, AccessFlags.Read);
                    builder.SetRenderAttachment(output, 0, AccessFlags.Write);
                    builder.SetRenderFunc(static (PassData passData, RasterGraphContext context) =>
                    {
                        RTHandle color = passData.source;
                        passData.properties.SetTexture(Source, color);
                        passData.properties.SetVector(ScaleBias, IdentityScaleBias);
                        passData.properties.SetVector(Sensor, passData.sensor);
                        context.cmd.DrawProcedural(Matrix4x4.identity, passData.material, 0,
                            MeshTopology.Triangles, 3, 1, passData.properties);
                    });
                }
                resources.cameraColor = output;
            }
        }
    }
}

using UnityEngine;
using UnityEngine.UI;

namespace DayOfDream.Horror
{
    // Camera-local state consumed by the render pass; the eyelids sit above all HUD elements.
    public sealed class PlayerConsciousness : MonoBehaviour
    {
        internal float Blur { get; private set; }
        private EyelidOverlay eyelids;

        internal void Initialize(HorrorHud hud)
        {
            var overlay = new GameObject("Eyelids", typeof(RectTransform), typeof(Canvas), typeof(CanvasRenderer), typeof(EyelidOverlay));
            var rect = (RectTransform)overlay.transform;
            rect.SetParent(hud.transform, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var canvas = overlay.GetComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = 100;
            eyelids = overlay.GetComponent<EyelidOverlay>();
            eyelids.raycastTarget = false;
            Set(0, 1);
        }

        internal void Set(float blur, float aperture)
        {
            Blur = Mathf.Clamp01(blur);
            if (eyelids != null) eyelids.SetOpening(aperture);
        }
    }

    [RequireComponent(typeof(CanvasRenderer))]
    internal sealed class EyelidOverlay : MaskableGraphic
    {
        private float opening = 1;
        internal void SetOpening(float value)
        {
            value = Mathf.Clamp01(value);
            if (Mathf.Approximately(opening, value)) return;
            opening = value;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            if (opening >= .999f) return;
            Rect rect = rectTransform.rect;
            const int strips = 64;
            for (int i = 0; i < strips; i++)
            {
                float x0 = (float)i / strips, x1 = (float)(i + 1) / strips;
                float gap0 = Gap(x0) * rect.height * .79f * opening;
                float gap1 = Gap(x1) * rect.height * .79f * opening;
                float left = Mathf.Lerp(rect.xMin, rect.xMax, x0);
                float right = Mathf.Lerp(rect.xMin, rect.xMax, x1);
                for (int side = -1; side <= 1; side += 2)
                {
                    float edge = side < 0 ? rect.yMin : rect.yMax;
                    float y0 = rect.center.y + side * gap0, y1 = rect.center.y + side * gap1;
                    float feather = Mathf.Min(24, rect.height * opening * .06f);
                    Quad(mesh, left, right, edge, edge, y0, y1, 1, 1);
                    Quad(mesh, left, right, y0, y1, y0 - side * feather, y1 - side * feather, 1, 0);
                }
            }
        }

        private static float Gap(float x) => Mathf.Sqrt(Mathf.Max(0, 1 - Mathf.Pow((x - .5f) * 1.5f, 2)));
        private static void Quad(VertexHelper mesh, float x0, float x1, float a0, float a1,
            float b0, float b1, float alphaA, float alphaB)
        {
            int n = mesh.currentVertCount;
            mesh.AddVert(new Vector3(x0, a0), new Color(0, 0, 0, alphaA), Vector2.zero);
            mesh.AddVert(new Vector3(x1, a1), new Color(0, 0, 0, alphaA), Vector2.zero);
            mesh.AddVert(new Vector3(x1, b1), new Color(0, 0, 0, alphaB), Vector2.zero);
            mesh.AddVert(new Vector3(x0, b0), new Color(0, 0, 0, alphaB), Vector2.zero);
            mesh.AddTriangle(n, n + 1, n + 2);
            mesh.AddTriangle(n, n + 2, n + 3);
        }
    }
}

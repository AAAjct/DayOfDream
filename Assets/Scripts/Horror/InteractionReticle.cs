using UnityEngine;
using UnityEngine.UI;

namespace DayOfDream.Horror
{
    [RequireComponent(typeof(CanvasRenderer))]
    internal sealed class InteractionReticle : MaskableGraphic
    {
        private const int Segments = 96;
        private bool focused;

        internal void SetFocused(bool value)
        {
            if (focused == value) return;
            focused = value;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            float radius = focused ? 15f : 3f;
            float innerRadius = focused ? 12f : 0;
            Color32 ink = color;

            if (focused)
            {
                float[] rings = { radius + .8f, radius, innerRadius, innerRadius - .8f };
                for (int i = 0; i < Segments; i++)
                {
                    float angle = 2 * Mathf.PI * i / Segments;
                    var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                    for (int ring = 0; ring < 4; ring++)
                    {
                        Color32 tint = ink;
                        if (ring is 0 or 3) tint.a = 0;
                        mesh.AddVert(direction * rings[ring], tint, Vector2.zero);
                    }
                }
                for (int i = 0; i < Segments; i++)
                    for (int ring = 0; ring < 3; ring++)
                    {
                        int current = i * 4 + ring, next = (i + 1) % Segments * 4 + ring;
                        mesh.AddTriangle(current, current + 1, next);
                        mesh.AddTriangle(current + 1, next + 1, next);
                    }
                return;
            }

            for (int i = 0; i < Segments; i++)
            {
                float angle = 2 * Mathf.PI * i / Segments;
                var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                mesh.AddVert(direction * radius, ink, Vector2.zero);
                mesh.AddVert(direction * innerRadius, ink, Vector2.zero);
            }

            for (int i = 0; i < Segments; i++)
            {
                int outer = i * 2;
                int next = (i + 1) % Segments * 2;
                mesh.AddTriangle(outer, outer + 1, next);
                if (focused) mesh.AddTriangle(outer + 1, next + 1, next);
            }
        }
    }
}

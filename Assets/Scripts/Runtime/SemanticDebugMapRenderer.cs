using System.Collections.Generic;
using UnityEngine;

namespace CityGenPedestrianProbe
{
    internal sealed class SemanticDebugMapRenderer
    {
        private Material material;

        public void Draw(SemanticSpaceMap map, Transform player, Rect screenRect,
            bool localView, float localSizeMeters, SemanticHit nearest)
        {
            if (map == null || player == null || Event.current.type != EventType.Repaint)
                return;
            EnsureMaterial();
            if (material == null)
                return;

            Vector2 center = localView
                ? new Vector2(player.position.x, player.position.z)
                : map.StudyCenterXZ;
            float worldSize = localView ? localSizeMeters : map.StudySizeMeters;
            Rect worldBounds = new Rect(center - Vector2.one * worldSize * 0.5f,
                Vector2.one * worldSize);

            GL.PushMatrix();
            material.SetPass(0);
            GL.LoadPixelMatrix(0f, Screen.width, Screen.height, 0f);
            DrawQuad(screenRect, new Color(0.025f, 0.03f, 0.04f, 0.96f));

            DrawKind(map.Features, SemanticSpaceKind.Road, worldBounds, screenRect,
                new Color(0.42f, 0.18f, 0.18f, 0.92f));
            DrawKind(map.Features, SemanticSpaceKind.Sidewalk, worldBounds, screenRect,
                new Color(0.14f, 0.45f, 0.68f, 0.9f));
            DrawKind(map.Features, SemanticSpaceKind.Curb, worldBounds, screenRect,
                new Color(1f, 0.58f, 0.08f, 0.96f));
            DrawKind(map.Features, SemanticSpaceKind.Crosswalk, worldBounds, screenRect,
                new Color(0.9f, 0.9f, 0.12f, 0.98f));

            Vector2 playerPoint = WorldToScreen(new Vector2(player.position.x, player.position.z),
                worldBounds, screenRect);
            if (nearest.Kind != SemanticSpaceKind.Unknown)
            {
                Vector2 target = WorldToScreen(new Vector2(nearest.NearestPointWorld.x,
                    nearest.NearestPointWorld.z), worldBounds, screenRect);
                DrawLine(playerPoint, target, 2f, new Color(1f, 1f, 1f, 0.7f));
                DrawCircle(target, 4f, Color.white);
            }

            DrawPlayer(playerPoint, player.eulerAngles.y);
            DrawBorder(screenRect, 2f, new Color(0.8f, 0.85f, 0.9f, 1f));
            GL.PopMatrix();
        }

        public void Dispose()
        {
            if (material != null)
                Object.Destroy(material);
            material = null;
        }

        private void EnsureMaterial()
        {
            if (material != null)
                return;
            Shader shader = Shader.Find("Hidden/Internal-Colored");
            if (shader == null)
                return;
            material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
            material.SetInt("_ZWrite", 0);
        }

        private static void DrawKind(IReadOnlyList<SemanticFeature> features,
            SemanticSpaceKind kind, Rect worldBounds, Rect screenRect, Color color)
        {
            GL.Begin(GL.TRIANGLES);
            GL.Color(color);
            for (int featureIndex = 0; featureIndex < features.Count; featureIndex++)
            {
                SemanticFeature feature = features[featureIndex];
                if (feature.Kind != kind || !feature.Bounds.Overlaps(worldBounds, true))
                    continue;
                IReadOnlyList<Vector2> vertices = feature.Vertices;
                IReadOnlyList<int> triangles = feature.Triangles;
                for (int i = 0; i + 2 < triangles.Count; i += 3)
                {
                    int ia = triangles[i];
                    int ib = triangles[i + 1];
                    int ic = triangles[i + 2];
                    if (ia < 0 || ib < 0 || ic < 0 || ia >= vertices.Count ||
                        ib >= vertices.Count || ic >= vertices.Count)
                        continue;
                    Vertex(WorldToScreen(vertices[ia], worldBounds, screenRect));
                    Vertex(WorldToScreen(vertices[ib], worldBounds, screenRect));
                    Vertex(WorldToScreen(vertices[ic], worldBounds, screenRect));
                }
            }
            GL.End();
        }

        private static Vector2 WorldToScreen(Vector2 world, Rect worldBounds, Rect screenRect)
        {
            float x = Mathf.InverseLerp(worldBounds.xMin, worldBounds.xMax, world.x);
            float y = Mathf.InverseLerp(worldBounds.yMin, worldBounds.yMax, world.y);
            return new Vector2(Mathf.Lerp(screenRect.xMin, screenRect.xMax, x),
                Mathf.Lerp(screenRect.yMax, screenRect.yMin, y));
        }

        private static void DrawPlayer(Vector2 center, float headingDegrees)
        {
            float radians = headingDegrees * Mathf.Deg2Rad;
            Vector2 forward = new Vector2(Mathf.Sin(radians), -Mathf.Cos(radians));
            Vector2 right = new Vector2(-forward.y, forward.x);
            Vector2 nose = center + forward * 12f;
            Vector2 left = center - forward * 7f - right * 7f;
            Vector2 rightPoint = center - forward * 7f + right * 7f;
            GL.Begin(GL.TRIANGLES);
            GL.Color(new Color(0.2f, 1f, 0.35f, 1f));
            Vertex(nose);
            Vertex(left);
            Vertex(rightPoint);
            GL.End();
            DrawCircle(center, 3f, Color.white);
        }

        private static void DrawCircle(Vector2 center, float radius, Color color)
        {
            const int segments = 16;
            GL.Begin(GL.TRIANGLES);
            GL.Color(color);
            for (int i = 0; i < segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                float b = (i + 1) * Mathf.PI * 2f / segments;
                Vertex(center);
                Vertex(center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius);
                Vertex(center + new Vector2(Mathf.Cos(b), Mathf.Sin(b)) * radius);
            }
            GL.End();
        }

        private static void DrawQuad(Rect rect, Color color)
        {
            GL.Begin(GL.QUADS);
            GL.Color(color);
            Vertex(new Vector2(rect.xMin, rect.yMin));
            Vertex(new Vector2(rect.xMax, rect.yMin));
            Vertex(new Vector2(rect.xMax, rect.yMax));
            Vertex(new Vector2(rect.xMin, rect.yMax));
            GL.End();
        }

        private static void DrawBorder(Rect rect, float thickness, Color color)
        {
            DrawLine(new Vector2(rect.xMin, rect.yMin), new Vector2(rect.xMax, rect.yMin), thickness, color);
            DrawLine(new Vector2(rect.xMax, rect.yMin), new Vector2(rect.xMax, rect.yMax), thickness, color);
            DrawLine(new Vector2(rect.xMax, rect.yMax), new Vector2(rect.xMin, rect.yMax), thickness, color);
            DrawLine(new Vector2(rect.xMin, rect.yMax), new Vector2(rect.xMin, rect.yMin), thickness, color);
        }

        private static void DrawLine(Vector2 a, Vector2 b, float thickness, Color color)
        {
            Vector2 normal = new Vector2(-(b - a).y, (b - a).x).normalized * thickness * 0.5f;
            GL.Begin(GL.QUADS);
            GL.Color(color);
            Vertex(a - normal);
            Vertex(a + normal);
            Vertex(b + normal);
            Vertex(b - normal);
            GL.End();
        }

        private static void Vertex(Vector2 point) => GL.Vertex3(point.x, point.y, 0f);
    }
}

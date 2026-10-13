using System.Collections.Generic;
using UnityEngine;

namespace RicochetBerlin
{
    /// <summary>
    /// Shared ricochet maths for real bullets and the aim preview, so the preview never lies.
    /// Bullets are swept with CircleCast every frame instead of using physics bounciness:
    /// no tunnelling through thin walls, no sticking in corners, exact reflection angles.
    /// </summary>
    public static class Ricochet
    {
        public const float Skin = 0.01f;

        static readonly RaycastHit2D[] Buffer = new RaycastHit2D[8];

        /// <summary>Nearest hit along the sweep, skipping <paramref name="ignore"/> (the shooter right after firing).</summary>
        public static bool Cast(Vector2 origin, float radius, Vector2 dir, float distance, int mask, Collider2D ignore, out RaycastHit2D hit)
        {
            var filter = new ContactFilter2D { useLayerMask = true, layerMask = mask, useTriggers = false };
            int count = Physics2D.CircleCast(origin, radius, dir, filter, Buffer, distance);
            hit = default;
            float best = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                if (Buffer[i].collider == ignore || Buffer[i].distance >= best) continue;
                best = Buffer[i].distance;
                hit = Buffer[i];
            }
            return best < float.MaxValue;
        }

        /// <summary>
        /// Path a bullet would take: start point, then every bounce point, then the end point.
        /// Stops at the first non-bounceable hit (a unit).
        /// </summary>
        public static void Trace(Vector2 origin, Vector2 dir, float radius, float maxDistance, int bounces,
                                 Collider2D ignore, List<Vector3> points)
        {
            points.Clear();
            points.Add(origin);
            float remaining = maxDistance;
            for (int i = 0; i <= bounces && remaining > 0f; i++)
            {
                if (!Cast(origin, radius, dir, remaining, Layers.Projectile, i == 0 ? ignore : null, out RaycastHit2D hit))
                {
                    points.Add(origin + dir * remaining);
                    return;
                }
                origin = hit.centroid + hit.normal * Skin;
                remaining -= hit.distance;
                points.Add(hit.centroid);
                int layer = hit.collider.gameObject.layer;
                if (layer == Layers.Player || layer == Layers.Enemy) return;
                dir = Vector2.Reflect(dir, hit.normal);
            }
        }
    }
}

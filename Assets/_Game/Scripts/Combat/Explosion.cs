using UnityEngine;

namespace RicochetBerlin
{
    /// <summary>Area damage with falloff + the matching visuals and sound.</summary>
    public static class Explosion
    {
        static readonly Collider2D[] Hits = new Collider2D[32];

        public static void Detonate(Vector2 center, float radius, float damage, float size = 1f, GameObject ignore = null)
        {
            var filter = new ContactFilter2D { useLayerMask = true, layerMask = Layers.Projectile, useTriggers = false };
            int count = Physics2D.OverlapCircle(center, radius, filter, Hits);
            // copy first: damage can chain into another explosion that reuses the shared buffer
            var targets = new System.Collections.Generic.HashSet<Health>();
            for (int i = 0; i < count; i++)
            {
                var h = Hits[i].GetComponentInParent<Health>();
                if (h != null && h.gameObject != ignore) targets.Add(h);
            }

            foreach (Health health in targets)
            {
                if (health == null || health.IsDead) continue;
                Vector2 to = (Vector2)health.transform.position - center;
                // walls shield units from the blast
                if (Physics2D.Linecast(center, health.transform.position, Layers.SightBlockers)) continue;
                float falloff = Mathf.Clamp01(1f - to.magnitude / radius) * 0.6f + 0.4f;
                health.TakeDamage(damage * falloff, to.normalized);
            }

            Fx.Instance?.Explosion(center, size);
            Audio.Play(Audio.Bank?.explosion, center, 0.9f * Mathf.Clamp(size, 0.6f, 1.2f), 1.1f - size * 0.15f);
            CameraRig.Instance?.AddTrauma(0.35f * size, center);
        }
    }
}

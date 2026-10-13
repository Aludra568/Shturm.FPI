using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace RicochetBerlin
{
    /// <summary>
    /// Pooled visual effects: sprite particles, light flashes, sound pings and ground decals.
    /// One small system instead of dozens of ParticleSystem prefabs - easy to read and tune in code.
    /// </summary>
    public class Fx : MonoBehaviour
    {
        public static Fx Instance { get; private set; }

        [Header("Materials")]
        [SerializeField] Material litMaterial;
        [SerializeField] Material unlitMaterial;

        [Header("Sprites")]
        [SerializeField] Sprite soft;
        [SerializeField] Sprite glow;
        [SerializeField] Sprite spark;
        [SerializeField] Sprite ring;
        [SerializeField] Sprite scorch;
        [SerializeField] Sprite muzzle;
        [SerializeField] Sprite[] explosionFrames;
        [SerializeField] Sprite[] smokeFrames;

        [Header("Pools")]
        [SerializeField] int particleCount = 500;
        [SerializeField] int flashCount = 16;
        [SerializeField] int decalCount = 60;

        class Particle
        {
            public SpriteRenderer sr;
            public Vector2 velocity;
            public float drag, life, age, size0, size1, spin;
            public Color color0, color1;
            public Sprite[] frames;
            public bool active;
        }

        class Flash
        {
            public Light2D light;
            public float life, age, intensity;
        }

        readonly List<Particle> particles = new();
        readonly List<Flash> flashes = new();
        readonly List<SpriteRenderer> decals = new();
        int nextParticle, nextFlash, nextDecal;

        void Awake()
        {
            Instance = this;
            for (int i = 0; i < particleCount; i++)
            {
                var go = new GameObject("p");
                go.transform.SetParent(transform, false);
                var sr = go.AddComponent<SpriteRenderer>();
                go.SetActive(false);
                particles.Add(new Particle { sr = sr });
            }
            int[] allLayers = AllSortingLayers();
            for (int i = 0; i < flashCount; i++)
            {
                var go = new GameObject("flash");
                go.transform.SetParent(transform, false);
                var light = go.AddComponent<Light2D>();
                light.lightType = Light2D.LightType.Point;
                light.shadowsEnabled = false;
                light.targetSortingLayers = allLayers;
                light.intensity = 0f;
                light.enabled = false;
                flashes.Add(new Flash { light = light });
            }
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public static int[] AllSortingLayers()
        {
            SortingLayer[] layers = SortingLayer.layers;
            var ids = new int[layers.Length];
            for (int i = 0; i < layers.Length; i++) ids[i] = layers[i].id;
            return ids;
        }

        // ------------------------------------------------------------------ high level effects

        public void MuzzleFlash(Vector2 pos, Vector2 dir)
        {
            Spawn(muzzle, pos + dir * 0.15f, dir * 1.5f, 0.07f, 1.4f, 1.8f,new Color(1f, 0.9f, 0.6f), new Color(1f, 0.5f, 0.1f, 0f), unlit: true, rotation: dir);
            for (int i = 0; i < 5; i++)
                Spawn(soft, pos, Rotate(dir, Random.Range(-35f, 35f)) * Random.Range(1f, 3f), Random.Range(0.3f, 0.6f), 0.25f, 0.7f,
                      new Color(0.7f, 0.7f, 0.7f, 0.5f), new Color(0.5f, 0.5f, 0.5f, 0f), drag: 4f);
            LightFlash(pos + dir * 0.4f, new Color(1f, 0.75f, 0.4f), 2.2f, 3.5f, 0.08f);
        }

        public void Ricochet(Vector2 point, Vector2 normal, Color color, int bounce)
        {
            Sparks(point, normal, color, 6 + bounce * 2, 5f);
            LightFlash(point, color, 1.1f, 1.8f, 0.07f);
            Audio.Play(Audio.Bank?.ricochet, point, 0.55f, 1f + bounce * 0.12f);
        }

        public void BulletImpact(Vector2 point, Vector2 normal, Color color, bool unitHit)
        {
            Sparks(point, normal, color, unitHit ? 10 : 7, unitHit ? 6f : 4f);
            Spawn(glow, point, Vector2.zero, 0.12f, 0.4f, 1.2f, color, new Color(color.r, color.g, color.b, 0f), unlit: true);
            if (!unitHit)
            {
                Dust(point, normal, 4);
                Audio.Play(Audio.Bank?.wallImpact, point, 0.6f);
            }
        }

        public void Explosion(Vector2 pos, float size)
        {
            Spawn(explosionFrames, pos, Vector2.zero, 0.45f, 1.4f * size, 1.9f * size, Color.white, new Color(1f, 1f, 1f, 0.6f), unlit: true);
            for (int i = 0; i < 7; i++)
            {
                Vector2 v = Random.insideUnitCircle * 2.2f * size;
                Spawn(smokeFrames, pos + v * 0.2f, v, Random.Range(0.8f, 1.4f), 0.6f * size, 1.6f * size,
                      new Color(0.5f, 0.48f, 0.45f, 0.9f), new Color(0.3f, 0.3f, 0.3f, 0f), drag: 2.5f);
            }
            Sparks(pos, Vector2.up, new Color(1f, 0.7f, 0.3f), 16, 9f * size, fullCircle: true);
            Spawn(ring, pos, Vector2.zero, 0.3f, 0.5f, 4.5f * size, new Color(1f, 0.85f, 0.6f, 0.8f), new Color(1f, 0.6f, 0.3f, 0f), unlit: true);
            LightFlash(pos, new Color(1f, 0.6f, 0.25f), 3.5f, 6f * size, 0.35f);
        }

        public void Sparks(Vector2 point, Vector2 normal, Color color, int count, float speed, bool fullCircle = false)
        {
            for (int i = 0; i < count; i++)
            {
                Vector2 dir = fullCircle ? Random.insideUnitCircle.normalized : Rotate(normal, Random.Range(-70f, 70f));
                Vector2 v = dir * speed * Random.Range(0.4f, 1.1f);
                Spawn(spark, point, v, Random.Range(0.12f, 0.3f), 0.35f, 0.1f, color, new Color(color.r, color.g * 0.5f, 0f, 0f),
                      drag: 6f, unlit: true, rotation: v);
            }
        }

        public void Dust(Vector2 point, Vector2 normal, int count)
        {
            for (int i = 0; i < count; i++)
                Spawn(soft, point, Rotate(normal, Random.Range(-60f, 60f)) * Random.Range(0.5f, 1.8f), Random.Range(0.4f, 0.8f), 0.2f, 0.7f,
                      new Color(0.55f, 0.5f, 0.45f, 0.6f), new Color(0.5f, 0.45f, 0.4f, 0f), drag: 3f);
        }

        /// <summary>Expanding ring that is visible through the darkness - "I hear something here".</summary>
        public void Ping(Vector2 pos, Color color, float size = 1.6f)
        {
            Spawn(ring, pos, Vector2.zero, 0.7f, 0.3f, size, color, new Color(color.r, color.g, color.b, 0f), unlit: true, order: SortOrder.Markers);
        }

        public void Scorch(Vector2 pos, float size) =>
            Decal(scorch, pos, Quaternion.Euler(0, 0, Random.Range(0, 360f)), size, new Color(1f, 1f, 1f, 0.85f));

        /// <summary>Long-lived ground mark (scorch, thrown track). Oldest ones are recycled.</summary>
        public void Decal(Sprite sprite, Vector2 pos, Quaternion rotation, float size, Color? color = null)
        {
            SpriteRenderer sr;
            if (decals.Count < decalCount)
            {
                var go = new GameObject("decal");
                go.transform.SetParent(transform, false);
                sr = go.AddComponent<SpriteRenderer>();
                sr.sharedMaterial = litMaterial;
                sr.sortingOrder = SortOrder.Decals + 1;
                decals.Add(sr);
            }
            else
            {
                sr = decals[nextDecal];
                nextDecal = (nextDecal + 1) % decals.Count;
            }
            sr.sprite = sprite;
            sr.color = color ?? Color.white;
            sr.transform.SetPositionAndRotation(pos, rotation);
            sr.transform.localScale = Vector3.one * size;
        }

        public void LightFlash(Vector2 pos, Color color, float intensity, float radius, float duration)
        {
            Flash f = flashes[nextFlash];
            nextFlash = (nextFlash + 1) % flashes.Count;
            f.light.transform.position = pos;
            f.light.color = color;
            f.light.pointLightOuterRadius = radius;
            f.light.pointLightInnerRadius = radius * 0.2f;
            f.light.intensity = intensity;
            f.light.enabled = true;
            f.intensity = intensity;
            f.life = duration;
            f.age = 0f;
        }

        // ------------------------------------------------------------------ particle core

        public void Spawn(Sprite sprite, Vector2 pos, Vector2 velocity, float life, float size0, float size1,
                          Color color0, Color color1, float drag = 0f, bool unlit = false, Vector2? rotation = null,
                          int order = SortOrder.Fx)
        {
            Particle p = Next();
            p.frames = null;
            Setup(p, sprite, pos, velocity, life, size0, size1, color0, color1, drag, unlit, rotation, order);
        }

        public void Spawn(Sprite[] frames, Vector2 pos, Vector2 velocity, float life, float size0, float size1,
                          Color color0, Color color1, float drag = 0f, bool unlit = false)
        {
            if (frames == null || frames.Length == 0) return;
            Particle p = Next();
            p.frames = frames;
            Setup(p, frames[0], pos, velocity, life, size0, size1, color0, color1, drag, unlit, null, SortOrder.Fx);
        }

        Particle Next()
        {
            Particle p = particles[nextParticle];
            nextParticle = (nextParticle + 1) % particles.Count;
            return p;
        }

        void Setup(Particle p, Sprite sprite, Vector2 pos, Vector2 velocity, float life, float size0, float size1,
                   Color color0, Color color1, float drag, bool unlit, Vector2? rotation, int order)
        {
            p.sr.sprite = sprite;
            p.sr.sharedMaterial = unlit ? unlitMaterial : litMaterial;
            p.sr.sortingOrder = order;
            p.sr.color = color0;
            p.velocity = velocity;
            p.drag = drag;
            p.life = Mathf.Max(0.01f, life);
            p.age = 0f;
            p.size0 = size0;
            p.size1 = size1;
            p.color0 = color0;
            p.color1 = color1;
            p.spin = rotation.HasValue ? 0f : Random.Range(-90f, 90f);
            float angle = rotation.HasValue ? Mathf.Atan2(rotation.Value.y, rotation.Value.x) * Mathf.Rad2Deg - 90f : Random.Range(0f, 360f);
            p.sr.transform.SetPositionAndRotation(pos, Quaternion.Euler(0, 0, angle));
            p.sr.transform.localScale = Vector3.one * size0;
            p.active = true;
            p.sr.gameObject.SetActive(true);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            foreach (Particle p in particles)
            {
                if (!p.active) continue;
                p.age += dt;
                float t = p.age / p.life;
                if (t >= 1f)
                {
                    p.active = false;
                    p.sr.gameObject.SetActive(false);
                    continue;
                }
                p.velocity *= Mathf.Max(0f, 1f - p.drag * dt);
                Transform tr = p.sr.transform;
                tr.position += (Vector3)(p.velocity * dt);
                if (p.spin != 0f) tr.Rotate(0, 0, p.spin * dt);
                tr.localScale = Vector3.one * Mathf.Lerp(p.size0, p.size1, t);
                p.sr.color = Color.Lerp(p.color0, p.color1, t);
                if (p.frames != null)
                    p.sr.sprite = p.frames[Mathf.Min(p.frames.Length - 1, (int)(t * p.frames.Length))];
            }

            foreach (Flash f in flashes)
            {
                if (!f.light.enabled) continue;
                f.age += dt;
                float t = f.age / f.life;
                if (t >= 1f) { f.light.enabled = false; continue; }
                f.light.intensity = f.intensity * (1f - t) * (1f - t);
            }
        }

        static Vector2 Rotate(Vector2 v, float degrees) => Quaternion.Euler(0, 0, degrees) * v;
    }
}

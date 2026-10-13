using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace RicochetBerlin
{
    /// <summary>Fades an object's sprites and lights in/out depending on whether the tank can see it.</summary>
    public class FogHidden : MonoBehaviour
    {
        [SerializeField] float radius = 0.4f;
        [SerializeField] float fadeSpeed = 7f;
        [Tooltip("Once spotted, stay visible (used by the flag).")]
        [SerializeField] bool stayRevealed;

        public bool IsVisible { get; private set; }
        public bool WasEverSeen { get; private set; }
        public event System.Action FirstSeen;

        readonly List<SpriteRenderer> sprites = new();
        readonly List<Color> spriteColors = new();
        readonly List<Light2D> lights = new();
        readonly List<float> lightIntensities = new();
        float alpha;

        void Awake()
        {
            foreach (var sr in GetComponentsInChildren<SpriteRenderer>(true))
            {
                sprites.Add(sr);
                spriteColors.Add(sr.color);
            }
            foreach (var l in GetComponentsInChildren<Light2D>(true))
            {
                lights.Add(l);
                lightIntensities.Add(l.intensity);
            }
            Apply(0f);
        }

        void LateUpdate()
        {
            bool visible = (stayRevealed && WasEverSeen) || (VisionSystem.Instance != null && VisionSystem.Instance.CanSee(transform.position, radius));
            if (visible && !WasEverSeen)
            {
                WasEverSeen = true;
                FirstSeen?.Invoke();
            }
            IsVisible = visible;
            float target = visible ? 1f : 0f;
            if (Mathf.Approximately(alpha, target)) return;
            Apply(Mathf.MoveTowards(alpha, target, fadeSpeed * Time.deltaTime));
        }

        /// <summary>The sprite's real colour, ignoring the current fade.</summary>
        public Color GetBaseColor(SpriteRenderer sr)
        {
            int i = sprites.IndexOf(sr);
            return i >= 0 ? spriteColors[i] : sr.color;
        }

        /// <summary>Call after changing a sprite's base colour at runtime (e.g. hit flash) so fading keeps it.</summary>
        public void SetBaseColor(SpriteRenderer sr, Color c)
        {
            int i = sprites.IndexOf(sr);
            if (i < 0) return;
            spriteColors[i] = c;
            sr.color = new Color(c.r, c.g, c.b, c.a * alpha);
        }

        void Apply(float a)
        {
            alpha = a;
            for (int i = 0; i < sprites.Count; i++)
            {
                if (sprites[i] == null) continue;
                Color c = spriteColors[i];
                sprites[i].color = new Color(c.r, c.g, c.b, c.a * a);
            }
            for (int i = 0; i < lights.Count; i++)
                if (lights[i] != null) lights[i].intensity = lightIntensities[i] * a;
        }
    }
}

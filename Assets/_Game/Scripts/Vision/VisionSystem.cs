using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace RicochetBerlin
{
    /// <summary>
    /// What the tank can see: a flashlight cone along the turret plus a small radius around the hull,
    /// both blocked by buildings (rubble is low - you see over it). Holding the right mouse button
    /// focuses the flashlight into a narrow, long beam. Light2D draws it; this class answers
    /// "is that point visible?" for hiding enemies, exactly matching the lights.
    /// </summary>
    public class VisionSystem : MonoBehaviour
    {
        public static VisionSystem Instance { get; private set; }

        [SerializeField] InputReader input;
        [SerializeField] Transform eye;
        [SerializeField] Light2D coneLight;
        [SerializeField] Light2D nearLight;
        [SerializeField] float coneAngle = 80f;
        [SerializeField] float coneRange = 11f;
        [Header("Focused beam (hold RMB)")]
        [SerializeField] float focusAngle = 32f;
        [SerializeField] float focusRange = 19f;
        [SerializeField] float focusIntensityBoost = 1.35f;
        [SerializeField] float focusSpeed = 6f;
        [SerializeField] float nearRadius = 2.8f;

        /// <summary>0 = wide cone, 1 = fully focused beam.</summary>
        public float Focus { get; private set; }
        public float CurrentAngle => Mathf.Lerp(coneAngle, focusAngle, Focus);
        public float CurrentRange => Mathf.Lerp(coneRange, focusRange, Focus);

        /// <summary>Tracers and explosions light up their surroundings and reveal enemies too.</summary>
        static readonly HashSet<RevealSource> Sources = new();

        public static void Register(RevealSource s) => Sources.Add(s);
        public static void Unregister(RevealSource s) => Sources.Remove(s);

        float baseIntensity;

        void Awake()
        {
            Instance = this;
            if (coneLight != null) baseIntensity = coneLight.intensity;
            ApplyToLights();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Update()
        {
            bool playing = GameManager.Instance == null || GameManager.Instance.IsPlaying;
            bool wantFocus = playing && input != null && input.FocusHeld;
            float focus = Mathf.MoveTowards(Focus, wantFocus ? 1f : 0f, focusSpeed * Time.deltaTime);
            if (focus == Focus) return;
            Focus = focus;
            ApplyToLights();
        }

        void ApplyToLights()
        {
            if (coneLight != null)
            {
                coneLight.pointLightOuterAngle = CurrentAngle;
                coneLight.pointLightInnerAngle = CurrentAngle * 0.55f;
                coneLight.pointLightOuterRadius = CurrentRange;
                if (Application.isPlaying) coneLight.intensity = baseIntensity * Mathf.Lerp(1f, focusIntensityBoost, Focus);
            }
            if (nearLight != null) nearLight.pointLightOuterRadius = nearRadius;
        }

        public bool CanSee(Vector2 point, float targetRadius = 0.3f)
        {
            Vector2 origin = eye.position;
            Vector2 to = point - origin;
            float dist = to.magnitude;

            bool inNear = dist <= nearRadius + targetRadius;
            bool inCone = dist <= CurrentRange + targetRadius && Vector2.Angle(eye.up, to) <= CurrentAngle * 0.5f + 4f;
            if ((inNear || inCone) && !Physics2D.Linecast(origin, point, Layers.SightBlockers)) return true;

            foreach (RevealSource s in Sources)
            {
                Vector2 sp = s.transform.position;
                if ((point - sp).sqrMagnitude <= s.Radius * s.Radius && !Physics2D.Linecast(sp, point, Layers.SightBlockers))
                    return true;
            }
            return false;
        }
    }
}

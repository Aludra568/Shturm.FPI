using System.Collections.Generic;
using UnityEngine;

namespace RicochetBerlin
{
    /// <summary>
    /// Smoke screen (the Wolf's ability): a thick cloud that hides the tank from drones while it is
    /// inside - gunners hold fire, kamikazes lose the trail and drift about.
    /// </summary>
    public class SmokeScreen : MonoBehaviour
    {
        static readonly List<SmokeScreen> Active = new();

        [SerializeField] float radius = 3.2f;
        [SerializeField] Sprite puff;
        [SerializeField] float puffInterval = 0.05f;

        float life;
        float age;
        float puffTimer;

        /// <summary>Is this point inside any smoke screen?</summary>
        public static bool Covers(Vector2 point)
        {
            foreach (SmokeScreen s in Active)
                if (Vector2.Distance(s.transform.position, point) <= s.radius * s.Strength) return true;
            return false;
        }

        /// <summary>The cloud billows out, holds, then thins away at the end.</summary>
        float Strength => Mathf.Clamp01(age / 0.6f) * Mathf.Clamp01((life - age) / 1.2f + 0.2f);

        public void Init(float seconds) => life = seconds;

        void OnEnable() => Active.Add(this);
        void OnDisable() => Active.Remove(this);

        void Start() => Audio.Play(Audio.Bank?.abilitySmoke, transform.position, 0.9f, 0.8f);

        void Update()
        {
            age += Time.deltaTime;
            if (age >= life) { Destroy(gameObject); return; }

            puffTimer -= Time.deltaTime;
            if (puffTimer > 0f || Fx.Instance == null) return;
            puffTimer = puffInterval;
            Vector2 pos = (Vector2)transform.position + Random.insideUnitCircle * radius * 0.8f * Strength;
            Fx.Instance.Spawn(puff, pos, Random.insideUnitCircle * 0.3f, Random.Range(1.6f, 2.4f), 1.2f, 2.6f,
                              new Color(0.42f, 0.44f, 0.47f, 0.55f), new Color(0.35f, 0.37f, 0.4f, 0f), drag: 0.5f, unlit: true);
        }

        void OnDrawGizmos()
        {
            Gizmos.color = Color.gray;
            Gizmos.DrawWireSphere(transform.position, radius);
        }
    }
}

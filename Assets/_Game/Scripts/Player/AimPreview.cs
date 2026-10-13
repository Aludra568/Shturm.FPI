using System.Collections.Generic;
using UnityEngine;

namespace RicochetBerlin
{
    /// <summary>
    /// Laser sight showing the shot up to the first ricochet plus a short stub of the bounce.
    /// Teaches the mechanic in seconds, but further bounces stay a skill to master.
    /// </summary>
    [RequireComponent(typeof(LineRenderer))]
    public class AimPreview : MonoBehaviour
    {
        [SerializeField] TankTurret turret;
        [SerializeField] float shellRadius = 0.1f;
        [SerializeField] float maxDistance = 30f;
        [SerializeField] float bounceStub = 2.5f;

        LineRenderer line;
        Collider2D hullCollider;
        readonly List<Vector3> points = new();

        void Awake()
        {
            line = GetComponent<LineRenderer>();
            line.useWorldSpace = true;
            hullCollider = GetComponentInParent<Rigidbody2D>().GetComponent<Collider2D>();
        }

        void LateUpdate()
        {
            bool playing = GameManager.Instance == null || GameManager.Instance.IsPlaying;
            line.enabled = playing;
            if (!playing) return;

            Vector2 origin = turret.transform.position;
            Ricochet.Trace(origin, turret.AimDirection, shellRadius, maxDistance, 1, hullCollider, points);

            // shorten the part after the first bounce to a hint
            if (points.Count == 3)
            {
                Vector3 bounce = points[1];
                Vector3 after = points[2] - bounce;
                points[2] = bounce + Vector3.ClampMagnitude(after, bounceStub);
            }

            line.positionCount = points.Count;
            for (int i = 0; i < points.Count; i++) line.SetPosition(i, points[i]);
        }
    }
}

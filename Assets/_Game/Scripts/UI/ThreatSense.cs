using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace RicochetBerlin
{
    /// <summary>
    /// "Spider sense": red chevrons behind the tank pointing at unseen drones creeping up from the
    /// rear (a 90 degree sector straight behind the gun, short range). Closer = brighter and faster
    /// pulse; an armed kamikaze gets a big chevron. A short warning sound plays when one gets close.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class ThreatSense : MonoBehaviour
    {
        [SerializeField] Camera cam;
        [SerializeField] float senseRange = 6f;
        [Tooltip("Full width of the rear sector that is sensed, degrees.")]
        [SerializeField] float rearSector = 90f;
        [SerializeField] float alarmRange = 4f;
        [Tooltip("Distance of the chevrons from the tank on screen, in UI pixels.")]
        [SerializeField] float ringRadius = 110f;
        [SerializeField] int maxChevrons = 8;

        VisualElement layer;
        readonly List<VisualElement> chevrons = new();
        readonly List<Drone> threats = new();
        readonly HashSet<Drone> alarmed = new();

        void Start()
        {
            var root = GetComponent<UIDocument>().rootVisualElement;
            layer = new VisualElement { pickingMode = PickingMode.Ignore };
            layer.AddToClassList("threat-layer");
            root.Q("hud").Add(layer);
            for (int i = 0; i < maxChevrons; i++)
            {
                var c = new VisualElement { pickingMode = PickingMode.Ignore };
                c.AddToClassList("threat-chevron");
                layer.Add(c);
                chevrons.Add(c);
            }
        }

        void LateUpdate()
        {
            GameManager gm = GameManager.Instance;
            bool active = gm != null && gm.IsPlaying && !gm.Player.Health.IsDead;
            Vector2 tank = active ? (Vector2)gm.Player.transform.position : Vector2.zero;
            Vector2 back = active ? -(Vector2)gm.Player.Turret.transform.up : Vector2.down;

            threats.Clear();
            if (active)
            {
                foreach (Drone d in Drone.Alive)
                {
                    float dist = Vector2.Distance(d.transform.position, tank);
                    if (!d.HiddenFromPlayer || dist > senseRange) continue;
                    if (Vector2.Angle(back, (Vector2)d.transform.position - tank) > rearSector * 0.5f) continue;
                    threats.Add(d);
                    if (dist < alarmRange && alarmed.Add(d))
                        Audio.Play(Audio.Bank?.threatSense, null, 0.55f, 0.9f);
                }
                alarmed.RemoveWhere(d => d == null || Vector2.Distance(d.transform.position, tank) > alarmRange + 1.5f);
                threats.Sort((a, b) => Vector2.Distance(a.transform.position, tank).CompareTo(Vector2.Distance(b.transform.position, tank)));
            }

            float w = layer.layout.width, h = layer.layout.height;
            Vector2 tankPanel = ToPanel(tank, w, h);
            for (int i = 0; i < chevrons.Count; i++)
            {
                var c = chevrons[i];
                if (i >= threats.Count || float.IsNaN(w))
                {
                    c.style.display = DisplayStyle.None;
                    continue;
                }

                Drone d = threats[i];
                Vector2 dir = ToPanel(d.transform.position, w, h) - tankPanel;
                if (dir.sqrMagnitude < 1f) dir = Vector2.down;
                dir.Normalize();

                float dist = Vector2.Distance(d.transform.position, tank);
                float near = 1f - Mathf.Clamp01(dist / senseRange);
                bool urgent = d is KamikazeDrone k && (k.Armed || k.Dashing);
                float pulse = 0.65f + 0.35f * Mathf.Sin(Time.time * Mathf.Lerp(4f, 16f, near));
                float size = (urgent ? 1.5f : 1f) * Mathf.Lerp(0.8f, 1.2f, near);

                Vector2 pos = tankPanel + dir * ringRadius;
                c.style.display = DisplayStyle.Flex;
                c.style.left = pos.x - 22f;
                c.style.top = pos.y - 22f;
                c.style.rotate = new Rotate(Mathf.Atan2(dir.x, -dir.y) * Mathf.Rad2Deg);
                c.style.scale = new Scale(Vector3.one * size);
                c.style.opacity = Mathf.Lerp(0.3f, 1f, near) * pulse;
            }
        }

        Vector2 ToPanel(Vector2 world, float w, float h)
        {
            Vector3 vp = cam.WorldToViewportPoint(world);
            return new Vector2(vp.x * w, (1f - vp.y) * h);
        }
    }
}

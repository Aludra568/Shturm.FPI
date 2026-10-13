using UnityEngine;

namespace RicochetBerlin
{
    public enum MarkerKind { PlayerStart, FlagSpot, EvacSpot, Hatch }

    /// <summary>Named point baked from the text map (flag candidates, evac candidates, manholes).</summary>
    public class MapMarker : MonoBehaviour
    {
        public MarkerKind kind;

        void OnDrawGizmos()
        {
            Gizmos.color = kind switch
            {
                MarkerKind.PlayerStart => Color.green,
                MarkerKind.FlagSpot => Color.red,
                MarkerKind.EvacSpot => Color.yellow,
                _ => Color.cyan
            };
            Gizmos.DrawWireSphere(transform.position, 0.45f);
        }
    }
}

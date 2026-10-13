using UnityEngine;

namespace RicochetBerlin
{
    /// <summary>Something whose armour reduces damage depending on where a shell lands.</summary>
    public interface IArmor
    {
        /// <summary>
        /// Damage factor for a hit at <paramref name="point"/> (surface normal pointing out of the target):
        /// 1 on a weak spot, less on armour.
        /// </summary>
        float DamageMultiplier(Vector2 hitNormal, Vector2 point);
    }
}

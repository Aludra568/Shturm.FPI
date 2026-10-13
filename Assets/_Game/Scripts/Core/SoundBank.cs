using UnityEngine;

namespace RicochetBerlin
{
    /// <summary>All sound clips in one asset, so designers swap sounds without touching code.</summary>
    [CreateAssetMenu(menuName = "Ricochet Berlin/Sound Bank")]
    public class SoundBank : ScriptableObject
    {
        public AudioClip[] tankShot;
        public AudioClip[] ricochet;
        public AudioClip[] wallImpact;
        public AudioClip[] unitHit;
        public AudioClip[] explosion;
        public AudioClip[] playerHit;
        public AudioClip[] armorHit;
        public AudioClip[] droneShot;
        public AudioClip[] hatchOpen;
        public AudioClip[] arming;
        public AudioClip[] nitro;
        public AudioClip[] radioBeep;
        public AudioClip[] flagPickup;
        public AudioClip[] alarm;
        public AudioClip[] win;
        public AudioClip[] lose;
        public AudioClip[] uiClick;
        public AudioClip[] repair;
        public AudioClip[] powerup;
        public AudioClip[] threatSense;
        public AudioClip[] abilitySmoke;
        public AudioClip[] abilityArmor;
        public AudioClip[] reloaded;
        public AudioClip engineLoop;
        public AudioClip droneLoop;
    }
}

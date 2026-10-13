using UnityEngine;

namespace RicochetBerlin
{
    /// <summary>Lights up a small area for the vision check (shell tracers, fires). Scout with ricochets!</summary>
    public class RevealSource : MonoBehaviour
    {
        [SerializeField] float radius = 1.6f;

        public float Radius => radius;

        void OnEnable() => VisionSystem.Register(this);
        void OnDisable() => VisionSystem.Unregister(this);
    }
}

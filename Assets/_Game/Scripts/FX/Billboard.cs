using UnityEngine;

namespace RicochetBerlin
{
    /// <summary>
    /// Keeps a sprite upright on screen while the camera rotates with the gun
    /// (flags, icons - things that look wrong upside down).
    /// </summary>
    public class Billboard : MonoBehaviour
    {
        void LateUpdate()
        {
            Camera cam = Camera.main;
            if (cam != null) transform.rotation = cam.transform.rotation;
        }
    }
}

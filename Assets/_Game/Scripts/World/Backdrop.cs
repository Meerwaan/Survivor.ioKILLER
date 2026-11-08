using UnityEngine;

namespace Survivor
{
    /// <summary>
    /// Far landmarks (the Six Towers of REGEN): they stay at the same distance north of the player, like a skyline,
    /// deep in the fog. The top-down game camera never sees them; the menu camera looks at them (Univers 2.6:
    /// visible from everywhere, like the sarcophagus from Pripyat).
    /// </summary>
    public class Backdrop : MonoBehaviour
    {
        public Vector3 offset = new(-12f, 0f, 170f);

        void LateUpdate()
        {
            var p = PlayerController.Instance;
            if (!p) return;
            Vector3 c = p.Position;
            transform.position = new Vector3(c.x + offset.x, offset.y, c.z + offset.z);
        }
    }
}

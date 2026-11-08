using UnityEngine;

namespace Survivor
{
    /// <summary>
    /// The Passeur's injector (Univers 9.8): the amber vial on the left forearm fills with the éclats burned toward
    /// the next level, pulses at every level, and is empty at the start of every run (the dose burns, nothing stays).
    /// The model's "Vial" bone is scaled along its length after the Animator ran (LateUpdate).
    /// </summary>
    public class InjectorVial : MonoBehaviour
    {
        [Tooltip("The bone that holds the vial's liquid (its local Y runs along the vial).")]
        public Transform vial;
        [Tooltip("Played on level-up (the Passeur taps the injector).")]
        public CharacterAnimator anim;

        float shown, pulse;
        int lastLevel = 1;

        void LateUpdate()
        {
            if (!vial) return;
            var xp = PlayerXP.Instance;
            float target = 0f;
            if (xp)
            {
                target = xp.Required > 0f ? Mathf.Clamp01(xp.Current / xp.Required) : 0f;
                if (xp.Level != lastLevel)
                {
                    if (xp.Level > lastLevel)
                    {
                        pulse = 1f;
                        shown = 1f; // full for a beat, then it drains into the body
                        if (anim) anim.Special();
                    }
                    lastLevel = xp.Level;
                }
            }
            shown = Mathf.MoveTowards(shown, target, Time.deltaTime * (shown > target ? 1.6f : 3f));
            pulse = Mathf.MoveTowards(pulse, 0f, Time.deltaTime * 2.5f);
            float fill = Mathf.Max(0.04f, shown);
            float bulge = 1f + pulse * 0.35f;
            vial.localScale = new Vector3(bulge, fill, bulge);
        }
    }
}

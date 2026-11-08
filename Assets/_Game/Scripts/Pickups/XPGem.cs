using System.Collections.Generic;
using UnityEngine;

namespace Survivor
{
    /// <summary>
    /// Dropped by enemies. Flies to the player once inside the pickup radius.
    /// Size and glow show its value: small éclat (1+), cluster (5+), big crystal (20+).
    /// Leans back and spins slowly so the shard faces the camera; GameFX draws a steady amber glow under each one.
    /// </summary>
    public class XPGem : MonoBehaviour
    {
        public static readonly List<XPGem> Active = new(256);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Active.Clear();

        public Renderer crystal;
        [Tooltip("Per tier: small éclat, cluster, big crystal (dark amber, amber, bright amber).")]
        public Material[] tierMaterials;
        [Tooltip("Optional mesh per tier (CORDON éclats). Empty = the same mesh, only bigger.")]
        public Mesh[] tierMeshes;

        /// <summary>0 small éclat, 1 cluster, 2 big crystal (read by GameFX for the glow).</summary>
        public int Tier { get; private set; }

        /// <summary>The modeled éclats read as thin slivers from the camera at their real size: oversized per tier.</summary>
        static readonly float[] ModeledScale = { 1.8f, 1.6f, 1.4f };
        const float SpinSpeed = 70f;
        /// <summary>Top leans away from the camera (~60° above, behind): the shard's long side faces it.</summary>
        static readonly Quaternion Lean = Quaternion.AngleAxis(14f, Vector3.right); // nearly upright: reads as a crystal, lying flat it read as a paper triangle

        float value, spin;
        bool attracted;
        float speed;
        Vector3 popVelocity;
        float popTime;
        int index = -1;

        /// <summary>Magnet pickup: every gem on the map flies to the player.</summary>
        public static void AttractAll()
        {
            foreach (var gem in Active) gem.attracted = true;
        }

        void OnEnable()
        {
            index = Active.Count;
            Active.Add(this);
        }

        void OnDisable()
        {
            int last = Active.Count - 1;
            if (index >= 0 && index <= last && Active[index] == this)
            {
                Active[index] = Active[last];
                Active[index].index = index;
                Active.RemoveAt(last);
            }
            index = -1;
        }

        public void Init(float xp)
        {
            value = xp;
            attracted = false;
            speed = -7f; // backs off a little before whooshing in
            spin = Random.Range(0f, 360f);
            Refresh();

            // Little jump out of the corpse.
            Vector2 r = Random.insideUnitCircle * 1.5f;
            popVelocity = new Vector3(r.x, Random.Range(4f, 6f), r.y);
            popTime = 0.45f;
        }

        /// <summary>Used to merge gems when too many are on the ground.</summary>
        public void AddValue(float xp)
        {
            value += xp;
            Refresh();
        }

        void Refresh()
        {
            int tier = value >= 20f ? 2 : value >= 5f ? 1 : 0;
            Tier = tier;
            if (crystal && tierMaterials != null && tier < tierMaterials.Length && tierMaterials[tier])
                crystal.sharedMaterial = tierMaterials[tier];
            if (crystal && tierMeshes != null && tier < tierMeshes.Length && tierMeshes[tier] &&
                crystal.TryGetComponent<MeshFilter>(out var mf))
                mf.sharedMesh = tierMeshes[tier];
            // CORDON éclats are modeled at their real size per tier; the greybox cube just grows.
            bool modeled = tierMeshes != null && tier < tierMeshes.Length && tierMeshes[tier];
            transform.localScale = Vector3.one * (modeled ? ModeledScale[tier] : 0.4f + 0.15f * tier);
        }

        void Update()
        {
            var player = PlayerController.Instance;
            if (!player) return;

            float dt = Time.deltaTime;
            spin += SpinSpeed * dt;
            transform.rotation = Lean * Quaternion.Euler(0f, spin, 0f);

            if (popTime > 0f && !attracted)
            {
                popTime -= dt;
                popVelocity.y -= 22f * dt;
                Vector3 pos = transform.position + popVelocity * dt;
                if (pos.y < 0.4f)
                {
                    pos.y = 0.4f;
                    popVelocity = Vector3.zero;
                }
                transform.position = pos;
            }

            Vector3 target = player.Position + Vector3.up * 0.6f;
            Vector3 toPlayer = target - transform.position;
            float distance = toPlayer.magnitude;

            if (!attracted && distance <= player.Stats.pickupRadius) attracted = true;
            if (!attracted) return;

            speed += 45f * dt;
            float step = (speed + 6f) * dt;
            transform.position += toPlayer.normalized * Mathf.Min(distance, step);

            if (distance < 0.6f)
            {
                PlayerXP.Instance.Add(value);
                SFX.PlayGem();
                Pool.Instance.Despawn(gameObject);
            }
        }
    }
}

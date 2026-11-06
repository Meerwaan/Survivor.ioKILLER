using System.Collections.Generic;
using UnityEngine;

namespace Survivor
{
    /// <summary>
    /// Reuses GameObjects instead of creating/destroying them. Essential with hundreds of enemies on mobile.
    /// </summary>
    public class Pool : MonoBehaviour
    {
        public static Pool Instance { get; private set; }

        readonly Dictionary<GameObject, Stack<GameObject>> free = new();
        readonly Dictionary<GameObject, GameObject> prefabOf = new();

        void Awake() => Instance = this;

        public GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            if (!free.TryGetValue(prefab, out var stack))
            {
                stack = new Stack<GameObject>();
                free[prefab] = stack;
            }

            if (stack.Count > 0)
            {
                var reused = stack.Pop();
                reused.transform.SetPositionAndRotation(position, rotation);
                reused.SetActive(true);
                return reused;
            }

            var created = Instantiate(prefab, position, rotation, transform);
            prefabOf[created] = prefab;
            return created;
        }

        public T Spawn<T>(GameObject prefab, Vector3 position, Quaternion rotation) where T : Component
            => Spawn(prefab, position, rotation).GetComponent<T>();

        public void Despawn(GameObject go)
        {
            if (!prefabOf.TryGetValue(go, out var prefab))
            {
                Destroy(go);
                return;
            }
            if (!go.activeSelf) return;

            go.SetActive(false);
            free[prefab].Push(go);
        }
    }
}

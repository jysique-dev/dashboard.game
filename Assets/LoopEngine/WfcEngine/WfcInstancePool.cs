using System.Collections.Generic;
using UnityEngine;

namespace LoopEngine.WfcEngine
{
    /// <summary>
    /// Pool de instancias por prefab. Con backtracking, la misma celda puede cambiar de
    /// modulo varias veces antes de asentarse, y destruir/instanciar en cada cambio es
    /// justo lo que hace que animar un volumen grande vaya a tirones.
    ///
    /// Las instancias devueltas se desactivan y quedan aparcadas bajo un contenedor propio.
    /// No se reutiliza una instancia de un prefab para otro: un pool unico obligaria a
    /// reconstruir la jerarquia, que es mas caro que instanciar.
    /// </summary>
    public sealed class WfcInstancePool
    {
        private readonly Dictionary<GameObject, Stack<GameObject>> free =
            new Dictionary<GameObject, Stack<GameObject>>();

        private readonly Dictionary<GameObject, GameObject> originOf =
            new Dictionary<GameObject, GameObject>();

        private readonly Transform parking;

        public int Reused { get; private set; }
        public int Created { get; private set; }
        public int Parked { get; private set; }

        public WfcInstancePool(Transform parking)
        {
            this.parking = parking;
        }

        public GameObject Get(GameObject prefab, Vector3 position, Quaternion rotation, Transform parent)
        {
            if (prefab == null) return null;

            if (free.TryGetValue(prefab, out var stack))
            {
                while (stack.Count > 0)
                {
                    var recycled = stack.Pop();
                    Parked--;

                    // Una instancia puede haber muerto por fuera (recarga de dominio,
                    // borrado manual). Se descarta en silencio y se prueba con la siguiente.
                    if (recycled == null) continue;

                    recycled.transform.SetParent(parent, false);
                    recycled.transform.SetPositionAndRotation(position, rotation);
                    recycled.SetActive(true);

                    Reused++;
                    return recycled;
                }
            }

            var instance = Object.Instantiate(prefab, position, rotation, parent);
            instance.hideFlags = HideFlags.DontSave;

            originOf[instance] = prefab;
            Created++;

            return instance;
        }

        public void Release(GameObject instance)
        {
            if (instance == null) return;

            if (!originOf.TryGetValue(instance, out var prefab))
            {
                // Sin origen conocido no se puede reutilizar sin arriesgar mezclar prefabs.
                DestroyInstance(instance);
                return;
            }

            if (!free.TryGetValue(prefab, out var stack))
            {
                stack = new Stack<GameObject>();
                free[prefab] = stack;
            }

            instance.SetActive(false);
            if (parking != null) instance.transform.SetParent(parking, false);

            stack.Push(instance);
            Parked++;
        }

        public void Dispose()
        {
            foreach (var pair in free)
            {
                while (pair.Value.Count > 0) DestroyInstance(pair.Value.Pop());
            }

            free.Clear();
            originOf.Clear();
            Parked = 0;
        }

        public string Describe() => $"pool: {Reused} reutilizadas, {Created} creadas, {Parked} aparcadas";

        private static void DestroyInstance(Object target)
        {
            if (target == null) return;

            if (Application.isPlaying) Object.Destroy(target);
            else Object.DestroyImmediate(target);
        }
    }
}
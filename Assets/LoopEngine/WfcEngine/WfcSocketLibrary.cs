using System;
using System.Collections.Generic;
using UnityEngine;
using LoopEngine.WfcEngine.Core;

namespace LoopEngine.WfcEngine
{
    /// <summary>
    /// Catalogo de sockets compartido por un set de modulos.
    /// Fuente unica de verdad de los ids: nadie mas los inventa.
    ///
    /// Principio: estabilidad de IDs sobre estabilidad de indices. Borrar o reordenar
    /// entradas nunca redirige silenciosamente una referencia existente; una referencia
    /// obsoleta se ve como "&lt;missing #N&gt;" en vez de apuntar a otro socket.
    /// </summary>
    [CreateAssetMenu(
        fileName = "WfcSocketLibrary",
        menuName = LoopRoutes.WFCSocketLibrary,
        order = 0)]
    public class WfcSocketLibrary : ScriptableObject
    {
        [SerializeField] private List<WfcSocketEntry> entries = new List<WfcSocketEntry>();

        [SerializeField, HideInInspector] private int nextId = 1;

        /// <summary>Se dispara cuando la libreria cambia en el editor. Para refresco en vivo de herramientas.</summary>
        public event Action Changed;

        public IReadOnlyList<WfcSocketEntry> Entries => entries;

        public int Count => entries.Count;

        public bool TryGetEntry(int id, out WfcSocketEntry entry)
        {
            if (id != WfcSocketDescriptor.InvalidId)
            {
                for (int i = 0; i < entries.Count; i++)
                {
                    if (entries[i] != null && entries[i].Id == id)
                    {
                        entry = entries[i];
                        return true;
                    }
                }
            }

            entry = null;
            return false;
        }

        public bool Contains(int id) => TryGetEntry(id, out _);

        /// <summary>Indice de lista de un id, o -1. Uso interno de herramientas; nunca serializar esto.</summary>
        public int IndexOfId(int id)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i] != null && entries[i].Id == id) return i;
            }
            return -1;
        }

        public string GetDisplayName(int id)
            => TryGetEntry(id, out var e) ? e.DisplayName : $"<missing #{id}>";

        /// <summary>Magenta cuando el id no existe: los datos rotos se ven, no se disimulan.</summary>
        public Color GetColor(int id)
            => TryGetEntry(id, out var e) ? e.Color : Color.magenta;

        public bool TryGetKind(int id, out WfcSocketKind kind)
        {
            if (TryGetEntry(id, out var e))
            {
                kind = e.Kind;
                return true;
            }

            kind = WfcSocketKind.HorizontalSymmetric;
            return false;
        }

        /// <summary>Resuelve un id + modificadores a un descriptor listo para el solver.</summary>
        public WfcSocketDescriptor Resolve(int id, bool flipped = false, int rotationIndex = 0)
            => TryGetEntry(id, out var e)
                ? e.ToDescriptor(flipped, rotationIndex)
                : WfcSocketDescriptor.Invalid;

        public WfcSocketEntry AddEntry(string displayName, WfcSocketKind kind)
        {
            var entry = new WfcSocketEntry(nextId++, displayName, kind, DefaultColorFor(kind));
            entries.Add(entry);
            RaiseChanged();
            return entry;
        }

        public bool RemoveEntry(int id)
        {
            int index = IndexOfId(id);
            if (index < 0) return false;

            entries.RemoveAt(index);
            RaiseChanged();
            return true;
        }

        public void RaiseChanged() => Changed?.Invoke();

        private static Color DefaultColorFor(WfcSocketKind kind)
        {
            switch (kind)
            {
                case WfcSocketKind.HorizontalSymmetric: return new Color(0.35f, 0.70f, 1.00f);
                case WfcSocketKind.HorizontalAsymmetric: return new Color(1.00f, 0.65f, 0.25f);
                case WfcSocketKind.VerticalInvariant: return new Color(0.45f, 0.90f, 0.55f);
                case WfcSocketKind.VerticalRotational: return new Color(0.85f, 0.50f, 0.95f);
                default: return Color.white;
            }
        }

        /// <summary>
        /// Invariantes que se fuerzan en cada edicion:
        /// 1. Toda entrada tiene un id valido y unico.
        /// 2. nextId siempre queda por encima del maximo id usado (no se reciclan ids).
        /// 3. Ninguna entrada queda sin nombre.
        /// </summary>
        private void OnValidate()
        {
            var seen = new HashSet<int>();
            int maxId = 0;

            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry == null)
                {
                    entry = new WfcSocketEntry();
                    entries[i] = entry;
                }

                // Id invalido (entrada nueva desde el inspector) o duplicado (por Duplicate Array Element).
                if (entry.Id == WfcSocketDescriptor.InvalidId || !seen.Add(entry.Id))
                {
                    entry.AssignId(nextId++);
                    seen.Add(entry.Id);
                }

                entry.EnsureName($"socket_{entry.Id}");
                if (entry.Id > maxId) maxId = entry.Id;
            }

            if (nextId <= maxId) nextId = maxId + 1;

            RaiseChanged();
        }
    }
}
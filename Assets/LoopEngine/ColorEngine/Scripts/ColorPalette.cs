using System;
using System.Collections.Generic;
using UnityEngine;

namespace LoopEngine.ColorEngine
{
    /// <summary>
    /// Coleccion de roles de color. Es el unico "dueno" de los colores del proyecto:
    /// highlights de grilla, ghosts, seleccion, UI, gizmos, etc.
    ///
    /// Puede haber varias paletas (una por zona/mundo, o una de daltonismo) e intercambiarse,
    /// igual que ya haces con CameraBounds.
    /// </summary>
    [CreateAssetMenu(fileName = "ColorPalette", menuName = LoopRoutes.ColorPalette )]
    public sealed class ColorPalette : ScriptableObject
    {
        [SerializeField] private List<ColorRole> roles = new List<ColorRole>();

        [NonSerialized] private Dictionary<string, Color> lookup;

        /// <summary>
        /// Se dispara cuando la paleta cambia (editar en la ventana, OnValidate, recarga).
        /// Los consumidores se suscriben en OnEnable y se desuscriben en OnDisable.
        /// </summary>
        public event Action Changed;

        public IReadOnlyList<ColorRole> Roles => roles;

        public int Count => roles.Count;

        public bool TryGetColor(string roleId, out Color color)
        {
            color = default;
            if (string.IsNullOrEmpty(roleId)) return false;

            EnsureLookup();
            return lookup.TryGetValue(roleId, out color);
        }

        public Color GetColor(string roleId, Color fallback)
        {
            return TryGetColor(roleId, out Color color) ? color : fallback;
        }

        public bool Contains(string roleId)
        {
            return TryGetColor(roleId, out _);
        }

        /// <summary>
        /// Reconstruye la cache y notifica. Llamar tras editar los roles por codigo
        /// (la ventana del editor ya lo hace).
        /// </summary>
        public void Invalidate()
        {
            lookup = null;
            Changed?.Invoke();
        }

        private void EnsureLookup()
        {
            if (lookup != null) return;

            lookup = new Dictionary<string, Color>(roles.Count, StringComparer.Ordinal);
            for (int i = 0; i < roles.Count; i++)
            {
                ColorRole role = roles[i];
                if (!role.IsValid) continue;

                // Si hay ids repetidos gana el primero; la ventana del editor los reporta.
                if (!lookup.ContainsKey(role.Id))
                    lookup.Add(role.Id, role.Color);
            }
        }

        private void OnEnable()
        {
            lookup = null;
        }

        private void OnValidate()
        {
            lookup = null;
            Changed?.Invoke();
        }
    }
}
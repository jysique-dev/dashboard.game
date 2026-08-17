using System;
using UnityEngine;

namespace LoopEngine.ColorEngine
{
    /// <summary>
    /// Campo serializable que reemplaza a un "public Color" suelto.
    /// En el Inspector se dibuja como un desplegable con los roles de la paleta
    /// (ver PaletteColorReferenceDrawer).
    ///
    /// Uso:
    ///   [SerializeField] private PaletteColorReference hoverColor;
    ///   ...
    ///   renderer.material.color = hoverColor.Value;
    /// </summary>
    [Serializable]
    public class PaletteColorReference
    {
        [SerializeField] private ColorPalette palette;
        [SerializeField] private string roleId = string.Empty;

        [Tooltip("Color usado si no hay paleta asignada o el rol no existe.")]
        [SerializeField] private Color fallbackColor = Color.magenta;

        public PaletteColorReference() { }

        public PaletteColorReference(ColorPalette palette, string roleId, Color fallbackColor)
        {
            this.palette = palette;
            this.roleId = roleId;
            this.fallbackColor = fallbackColor;
        }

        public ColorPalette Palette => palette;
        public string RoleId => roleId;
        public Color FallbackColor => fallbackColor;

        /// <summary>True si el rol existe realmente en la paleta asignada.</summary>
        public bool IsResolved => palette != null && palette.Contains(roleId);

        /// <summary>Color final. Nunca lanza: cae al fallback si algo falta.</summary>
        public Color Value
        {
            get
            {
                if (palette != null && palette.TryGetColor(roleId, out Color color))
                    return color;
                return fallbackColor;
            }
        }

        /// <summary>Reasignacion en runtime (por ejemplo al cambiar de zona/mundo).</summary>
        public void Set(ColorPalette newPalette, string newRoleId)
        {
            palette = newPalette;
            roleId = newRoleId;
        }
    }
}
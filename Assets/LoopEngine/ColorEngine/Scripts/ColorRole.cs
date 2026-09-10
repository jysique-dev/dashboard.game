using System;
using UnityEngine;

namespace LoopEngine.ColorEngine
{
    /// <summary>
    /// Entrada de una <see cref="ColorPalette"/>: un identificador semantico y su color.
    /// El id es la unica cosa que referencian los consumidores, por eso el color puede
    /// cambiar en un solo sitio y propagarse a todo el proyecto.
    /// Convencion sugerida: "Grid/Hover", "Grid/GhostValid", "Selection/Primary".
    /// La barra "/" agrupa los roles en submenus dentro del desplegable del Inspector.
    /// </summary>
    [Serializable]
    public struct ColorRole
    {
        [SerializeField] private string id;
        [SerializeField] private Color color;

        public ColorRole(string id, Color color)
        {
            this.id = id;
            this.color = color;
        }

        public string Id => id;
        public Color Color => color;

        public bool IsValid => !string.IsNullOrWhiteSpace(id);
    }
}
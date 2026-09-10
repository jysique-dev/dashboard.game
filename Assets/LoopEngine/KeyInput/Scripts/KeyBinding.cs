using System;
using UnityEngine;

namespace LoopEngine.TagInput
{
    /// <summary>
    /// Asociación serializable entre un identificador de texto (tag) y una tecla física (KeyCode).
    /// Es un tipo de datos puro: no consulta input ni depende de Unity más allá de la serialización.
    /// </summary>
    [Serializable]
    public class KeyBinding
    {
        [Tooltip("Identificador de texto con el que el resto del código pide esta tecla. Ej: \"Jump\".")]
        [SerializeField] private string tag = string.Empty;

        [Tooltip("Tecla física asociada al tag.")]
        [SerializeField] private KeyCode key = KeyCode.None;

        //[Tooltip("Nota opcional para el equipo. No se usa en tiempo de ejecución.")]
        //[SerializeField] private string description = string.Empty;

        /// <summary>Identificador de texto del binding.</summary>
        public string Tag => tag;

        /// <summary>Tecla física asociada.</summary>
        public KeyCode Key => key;

        ///// <summary>Nota descriptiva opcional.</summary>
        //public string Description => description;

        public KeyBinding()
        {
        }

        public KeyBinding(string tag, KeyCode key, string description = "")
        {
            this.tag = tag;
            this.key = key;
            //this.description = description ?? string.Empty;
        }

        /// <summary>
        /// Un binding es válido si tiene un tag con contenido y una tecla distinta de None.
        /// Los bindings inválidos se descartan al construir el mapa y se reportan por consola.
        /// </summary>
        public bool IsValid =>
            !string.IsNullOrWhiteSpace(tag) && key != KeyCode.None;

        public override string ToString() =>
            $"\"{tag}\" -> {key}";
    }
}
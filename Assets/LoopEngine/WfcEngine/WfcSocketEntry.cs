using System;
using UnityEngine;
using LoopEngine.WfcEngine.Core;

namespace LoopEngine.WfcEngine
{
    /// <summary>
    /// Entrada de la libreria de sockets: la definicion de autoria de un socket.
    /// El Id es estable y no cambia nunca; el nombre y el color son solo presentacion.
    /// </summary>
    [Serializable]
    public class WfcSocketEntry
    {
        [SerializeField, HideInInspector] private int id = WfcSocketDescriptor.InvalidId;

        [Tooltip("Nombre legible. Solo presentacion: cambiarlo no rompe referencias.")]
        [SerializeField] private string displayName = "socket";

        [Tooltip("Color para gizmos y herramientas de editor.")]
        [SerializeField] private Color color = Color.white;

        [SerializeField] private WfcSocketKind kind = WfcSocketKind.HorizontalSymmetric;

        [Tooltip("Notas de autoria. No afecta a la generacion.")]
        [SerializeField, TextArea(1, 3)] private string notes = string.Empty;

        public int Id => id;
        public string DisplayName => displayName;
        public Color Color => color;
        public WfcSocketKind Kind => kind;
        public string Notes => notes;

        public bool IsHorizontal => WfcSocketKinds.IsHorizontal(kind);
        public bool IsVertical => WfcSocketKinds.IsVertical(kind);

        public WfcSocketEntry() { }

        public WfcSocketEntry(int id, string displayName, WfcSocketKind kind, Color color)
        {
            this.id = id;
            this.displayName = displayName;
            this.kind = kind;
            this.color = color;
        }

        /// <summary>Solo la libreria asigna ids. Nadie mas debe llamar a esto.</summary>
        internal void AssignId(int newId) => id = newId;

        internal void EnsureName(string fallback)
        {
            if (string.IsNullOrWhiteSpace(displayName)) displayName = fallback;
        }

        /// <summary>Descriptor base, sin modificadores aplicados.</summary>
        public WfcSocketDescriptor ToDescriptor(bool flipped = false, int rotationIndex = 0)
            => new WfcSocketDescriptor(id, kind, flipped, rotationIndex);
    }
}
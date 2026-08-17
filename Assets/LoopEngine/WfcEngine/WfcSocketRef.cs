using System;
using UnityEngine;
using LoopEngine.WfcEngine.Core;

namespace LoopEngine.WfcEngine
{
    /// <summary>
    /// Referencia serializable a un socket de una libreria, mas los modificadores
    /// de la cara concreta donde se usa (flipped / indice de rotacion).
    ///
    /// Guarda el id, nunca el indice de lista. Si el socket desaparece, la referencia
    /// se resuelve a Invalid y las herramientas lo muestran como faltante.
    /// </summary>
    [Serializable]
    public struct WfcSocketRef
    {
        [Tooltip("Libreria propietaria del socket.")]
        public WfcSocketLibrary library;

        [Tooltip("Id estable dentro de la libreria. 0 = sin socket.")]
        public int socketId;

        [Tooltip("Solo aplica a sockets horizontales asimetricos.")]
        public bool flipped;

        [Tooltip("0..3. Solo aplica a sockets verticales rotacionales.")]
        public int rotationIndex;

        public bool HasLibrary => library != null;

        public bool IsAssigned => socketId != WfcSocketDescriptor.InvalidId;

        /// <summary>Asignado y ademas presente en la libreria.</summary>
        public bool IsResolvable => HasLibrary && library.Contains(socketId);

        public bool TryResolve(out WfcSocketDescriptor descriptor)
        {
            if (HasLibrary && library.TryGetEntry(socketId, out var entry))
            {
                descriptor = entry.ToDescriptor(flipped, rotationIndex);
                return true;
            }

            descriptor = WfcSocketDescriptor.Invalid;
            return false;
        }

        /// <summary>Descriptor resuelto, o Invalid si la referencia esta rota.</summary>
        public WfcSocketDescriptor Descriptor
            => TryResolve(out var d) ? d : WfcSocketDescriptor.Invalid;

        public string DisplayName
        {
            get
            {
                if (!IsAssigned) return "(vacio)";
                if (!HasLibrary) return $"<sin libreria #{socketId}>";
                return library.Contains(socketId)
                    ? $"{library.GetDisplayName(socketId)} [{Descriptor.Notation()}]"
                    : $"<missing #{socketId}>";
            }
        }

        public Color DisplayColor
        {
            get
            {
                if (!IsAssigned) return new Color(0.5f, 0.5f, 0.5f, 0.5f);
                return HasLibrary ? library.GetColor(socketId) : Color.magenta;
            }
        }

        /// <summary>Normaliza modificadores irrelevantes segun el tipo real del socket.</summary>
        public void Normalize()
        {
            rotationIndex = ((rotationIndex % 4) + 4) % 4;

            if (HasLibrary && library.TryGetKind(socketId, out var kind))
            {
                if (!WfcSocketKinds.UsesFlipped(kind)) flipped = false;
                if (!WfcSocketKinds.UsesRotationIndex(kind)) rotationIndex = 0;
            }
        }
    }
}
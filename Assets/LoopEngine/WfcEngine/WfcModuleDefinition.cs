using System.Collections.Generic;
using UnityEngine;
using LoopEngine.WfcEngine.Core;

namespace LoopEngine.WfcEngine
{
    /// <summary>
    /// Un modulo: el prefab que se instancia en una celda, mas el socket de cada una
    /// de sus 6 caras. Es el dato de autoria; el solver nunca ve este asset, solo la
    /// version horneada (sesion 3 en adelante).
    ///
    /// Un modulo sin prefab es legal y significa "aire": ocupa la celda, tiene sockets,
    /// pero no instancia geometria. Es el equivalente al empty tile de marian42 y hace
    /// falta para que el borde del volumen tenga algo que colapsar.
    /// </summary>
    [CreateAssetMenu(
        fileName = "WfcModule",
        menuName = "LoopEngine/WFC/Module Definition",
        order = 1)]
    public class WfcModuleDefinition : ScriptableObject
    {
        [Tooltip("Geometria a instanciar. Vacio = modulo de aire.")]
        [SerializeField] private GameObject prefab;

        [Tooltip("Libreria de la que salen los sockets de las 6 caras.")]
        [SerializeField] private WfcSocketLibrary library;

        [Tooltip("Sockets indexados por WfcDirection: 0 +X, 1 -X, 2 +Y, 3 -Y, 4 +Z, 5 -Z.")]
        [SerializeField] private WfcSocketRef[] faces = new WfcSocketRef[WfcDirections.Count];

        [Tooltip("Peso relativo al elegir entre modulos posibles. 0 = nunca se elige salvo que sea el unico.")]
        [SerializeField, Min(0f)] private float weight = 1f;

        [Tooltip("Giros de 90 grados permitidos. Declara solo Rot0 si el modulo es rotacionalmente simetrico.")]
        [SerializeField] private WfcRotationMask allowedRotations = WfcRotationMask.All;

        [SerializeField, TextArea(1, 4)] private string notes = string.Empty;

        public GameObject Prefab => prefab;
        public WfcSocketLibrary Library => library;
        public float Weight => weight;
        public WfcRotationMask AllowedRotations => allowedRotations;
        public string Notes => notes;

        public IReadOnlyList<WfcSocketRef> Faces => faces;

        /// <summary>Modulo de aire: valido, sin geometria.</summary>
        public bool IsAir => prefab == null;

        public WfcSocketRef GetFace(WfcDirection direction)
        {
            int index = (int)direction;
            return (faces != null && index >= 0 && index < faces.Length)
                ? faces[index]
                : default;
        }

        /// <summary>Socket resuelto de una cara, sin rotar. Invalid si la referencia esta rota.</summary>
        public WfcSocketDescriptor GetDescriptor(WfcDirection direction)
            => GetFace(direction).Descriptor;

        /// <summary>Las 6 caras ya resueltas, en orden de WfcDirection.</summary>
        public WfcSocketDescriptor[] ResolveDescriptors()
        {
            var result = new WfcSocketDescriptor[WfcDirections.Count];
            for (int i = 0; i < WfcDirections.Count; i++)
            {
                result[i] = GetDescriptor((WfcDirection)i);
            }
            return result;
        }

        public bool HasAllFacesAssigned()
        {
            for (int i = 0; i < WfcDirections.Count; i++)
            {
                if (!GetFace((WfcDirection)i).IsResolvable) return false;
            }
            return true;
        }

        /// <summary>
        /// Invariantes de autoria:
        /// 1. El array de caras mide exactamente 6.
        /// 2. Toda referencia hereda la libreria del modulo si no tiene una.
        /// 3. Los modificadores irrelevantes se anulan (via WfcSocketRef.Normalize).
        /// No se corrige aqui el socket puesto en la cara equivocada: eso se reporta
        /// como error en WfcModuleValidation para que el autor lo vea, no se disimule.
        /// </summary>
        private void OnValidate()
        {
            if (faces == null || faces.Length != WfcDirections.Count)
            {
                var resized = new WfcSocketRef[WfcDirections.Count];
                if (faces != null)
                {
                    int copy = Mathf.Min(faces.Length, resized.Length);
                    for (int i = 0; i < copy; i++) resized[i] = faces[i];
                }
                faces = resized;
            }

            for (int i = 0; i < faces.Length; i++)
            {
                var face = faces[i];
                if (face.library == null && library != null) face.library = library;
                face.Normalize();
                faces[i] = face;
            }

            if (weight < 0f) weight = 0f;
        }
    }
}
using System;
using UnityEngine;

namespace LoopEngine.Core
{
    /// <summary>
    /// Base NO generica de OptionalRef. Existe por dos motivos concretos:
    ///
    ///   1. CustomPropertyDrawer necesita un Type cerrado. Registrar el drawer sobre
    ///      OptionalRef&lt;&gt; abierto no es fiable; sobre esta base con useForChildren:true si.
    ///      Es el mismo motivo por el que UnityEvent obliga a subclases concretas.
    ///   2. El validador puede recorrer campos sin reflexionar sobre genericos: le basta
    ///      con castear a OptionalRefBase.
    ///
    /// Los campos serializados viven AQUI, no en la subclase generica. Unity serializa los
    /// campos heredados de una clase [Serializable], y la polimorfia no es problema porque
    /// el tipo declarado del campo es siempre la subclase concreta.
    /// </summary>
    [Serializable]
    public abstract class OptionalRefBase
    {
        [SerializeField] protected bool enabled = true;

        // Tipado como UnityEngine.Object, no como MonoBehaviour: asi tambien admite
        // ScriptableObjects que implementen la interfaz. El filtrado por interfaz lo hace
        // el drawer, no el tipo del campo (Unity no serializa campos de tipo interfaz).
        [SerializeField] protected UnityEngine.Object target;

        /// <summary>Si esta apag   ado, el consumidor debe comportarse como si la dependencia no existiera.</summary>
        public bool Enabled => enabled;

        public bool IsAssigned => target != null;

        /// <summary>Objeto crudo. Para el drawer y el validador; los consumidores usan TryGet.</summary>
        public UnityEngine.Object RawTarget => target;

        /// <summary>Interfaz o clase que el objeto asignado debe implementar.</summary>
        public abstract Type RequiredType { get; }

        /// <summary>
        /// Activado pero sin asignar. ESTA es la unica condicion de error: separa
        /// "no quiero esta funcion" (apagado) de "me olvide de cablearla" (esto).
        /// Antes ambos casos eran un null indistinguible y fallaban en silencio.
        /// </summary>
        public bool IsMisconfigured => enabled && target == null;
    }

    /// <summary>
    /// Referencia opcional y tipada por interfaz, asignable desde el inspector.
    ///
    /// USO: declara una subclase concreta por interfaz (patron UnityEvent):
    ///
    ///     [Serializable]
    ///     public sealed class TerrainHeightRef : OptionalRef&lt;ITerrainHeightSource&gt; { }
    ///
    ///     [SerializeField] private TerrainHeightRef terrainHeight;
    ///     ...
    ///     if (terrainHeight.TryGet(out ITerrainHeightSource heights)) { ... }
    ///
    /// Las tres lineas de boilerplate por interfaz son el precio de que el drawer funcione
    /// de forma fiable. Un drawer sobre el generico abierto se ahorraria la subclase pero
    /// depende de detalles de version de Unity.
    /// </summary>
    [Serializable]
    public class OptionalRef<T> : OptionalRefBase where T : class
    {
        // El cast se cachea, pero NO el chequeo de nulidad: un objeto destruido sigue
        // devolviendo una referencia C# viva a traves de 'as' (el operador == de Unity es
        // el que detecta la destruccion, y 'as' lo esquiva).
        [NonSerialized] private T _cached;
        [NonSerialized] private bool _resolved;

        public override Type RequiredType => typeof(T);

        public OptionalRef() { }

        public OptionalRef(UnityEngine.Object value, bool isEnabled = true)
        {
            target = value;
            enabled = isEnabled;
        }

        /// <summary>Valor resuelto, o null si esta apagado, sin asignar o destruido.</summary>
        public T Value
        {
            get
            {
                TryGet(out T value);
                return value;
            }
        }

        /// <summary>
        /// True solo si esta activado Y hay un objeto vivo que implementa T.
        /// Devolver false en vez de lanzar es deliberado: el consumidor decide si eso es
        /// un fallo o una funcion desactivada a proposito.
        /// </summary>
        public bool TryGet(out T value)
        {
            if (!enabled || target == null)
            {
                value = null;
                return false;
            }

            if (!_resolved)
            {
                _cached = target as T;
                _resolved = true;
            }

            value = _cached;
            return value != null;
        }

        /// <summary>Asignacion por codigo. Invalida el cache del cast.</summary>
        public void Set(UnityEngine.Object value)
        {
            target = value;
            _resolved = false;
            _cached = null;
        }

        public void SetEnabled(bool value) => enabled = value;
    }
}
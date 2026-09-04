using LoopEngine.LogEngine.Core;
using System;
using System.Collections.Generic;
using UnityEditor.PackageManager;
using UnityEngine;
using LogLevel = LoopEngine.LogEngine.Core.LogLevel;


namespace LoopEngine.LogEngine
{
    /// <summary>
    /// Configuracion del sistema de logs. Implementa <see cref="ILogChannelFilter"/>,
    /// asi que el asset ES el filtro que consume el router (mismo patron que
    /// CameraSettings / GridSettings / ColorPalette).
    /// </summary>
    [CreateAssetMenu(fileName = "LogSettings", menuName = LoopRoutes.LoogerRoute + "/Log Settings")]
    public sealed class LogSettings : ScriptableObject, ILogChannelFilter
    {
        [Serializable]
        public sealed class ChannelConfig
        {
            public string Id = LogChannels.General;
            public bool Enabled = true;

            [Tooltip("Nivel minimo que deja pasar este canal.")]
            public LogLevel MinimumLevel = LogLevel.Trace;

            [Tooltip("Color del prefijo [Canal] en la consola del editor.")]
            public Color Color = new Color(0.60f, 0.78f, 1f);
        }

        [Header("Global")]
        [SerializeField]
        [Tooltip("Corte global aplicado antes que el filtro por canal.")]
        private LogLevel _globalMinimumLevel = LogLevel.Trace;

        [SerializeField]
        [Tooltip("Si un canal no esta en la lista, ¿se muestra igual? " +
                 "Recomendado true: asi un modulo nuevo no pierde sus logs por olvido.")]
        private bool _allowUnknownChannels = true;

        [Header("Formato")]
        [SerializeField]
        [Tooltip("Colorea el prefijo con rich text. Solo tiene efecto en el editor.")]
        private bool _useColor = true;

        [SerializeField]
        [Tooltip("Antepone el numero de frame: [f:1234].")]
        private bool _showFrame = false;

        [Header("Canales")]
        [SerializeField]
        private List<ChannelConfig> _channels = new List<ChannelConfig>();

        private Dictionary<string, ChannelConfig> _lookup;

        /// <summary>Se dispara al editar el asset. Lo consumira la ventana del editor (sesion L2).</summary>
        public event Action Changed;

        public LogLevel GlobalMinimumLevel => _globalMinimumLevel;
        public bool UseColor => _useColor;
        public bool ShowFrame => _showFrame;
        public IReadOnlyList<ChannelConfig> Channels => _channels;

        // --- ILogChannelFilter ------------------------------------------------

        public bool IsEnabled(string channel, LogLevel level)
        {
            if (level < _globalMinimumLevel) return false;

            if (!TryGetChannel(channel, out var config))
                return _allowUnknownChannels;

            return config.Enabled && level >= config.MinimumLevel;
        }

        // --- Consulta ---------------------------------------------------------

        public bool TryGetChannel(string id, out ChannelConfig config)
        {
            EnsureLookup();
            return _lookup.TryGetValue(id ?? string.Empty, out config);
        }

        public Color GetChannelColor(string id)
        {
            return TryGetChannel(id, out var config) ? config.Color : Color.white;
        }

        // --- Ciclo de vida ----------------------------------------------------

        // Patron idempotente igual que EnsureMap()/EnsureSubscribed(): el diccionario
        // se reconstruye solo cuando hace falta, no en cada acceso.
        private void EnsureLookup()
        {
            if (_lookup != null) return;

            _lookup = new Dictionary<string, ChannelConfig>(_channels.Count, StringComparer.Ordinal);
            for (int i = 0; i < _channels.Count; i++)
            {
                var config = _channels[i];
                if (config == null || string.IsNullOrEmpty(config.Id)) continue;
                _lookup[config.Id] = config; // el ultimo gana si hay ids duplicados
            }
        }

        private void OnEnable() => _lookup = null;

        private void OnValidate()
        {
            _lookup = null;
            Changed?.Invoke();
        }

        /// <summary>Rellena la lista con los canales de <see cref="LogChannels"/> que falten.</summary>
        public void AddMissingDefaultChannels()
        {
            EnsureLookup();
            AddIfMissing(LogChannels.General, new Color(0.75f, 0.75f, 0.75f));
            AddIfMissing(LogChannels.Camera, new Color(0.55f, 0.85f, 1.00f));
            AddIfMissing(LogChannels.Grid, new Color(0.55f, 1.00f, 0.70f));
            AddIfMissing(LogChannels.Placement, new Color(0.80f, 1.00f, 0.55f));
            AddIfMissing(LogChannels.Pathfinding, new Color(1.00f, 0.90f, 0.50f));
            AddIfMissing(LogChannels.Wfc, new Color(0.85f, 0.65f, 1.00f));
            AddIfMissing(LogChannels.Terrain, new Color(0.90f, 0.75f, 0.50f));
            AddIfMissing(LogChannels.Color, new Color(1.00f, 0.65f, 0.80f));
            AddIfMissing(LogChannels.EditorTools, new Color(0.70f, 0.70f, 1.00f));
            _lookup = null;
        }

        private void AddIfMissing(string id, Color color)
        {
            if (_lookup.ContainsKey(id)) return;
            var config = new ChannelConfig { Id = id, Color = color };
            _channels.Add(config);
            _lookup[id] = config;
        }
    }
}
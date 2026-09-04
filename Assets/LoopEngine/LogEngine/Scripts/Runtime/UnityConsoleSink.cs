using LoopEngine.LogEngine.Core;
using System.Text;
using UnityEditor.PackageManager;
using UnityEngine;
using UnityObject = UnityEngine.Object;
using LogLevel = LoopEngine.LogEngine.Core.LogLevel;

namespace LoopEngine.LogEngine
{
    /// <summary>
    /// Escribe las entradas en la consola de Unity. Es el unico punto del modulo
    /// que llama a <c>UnityEngine.Debug</c>.
    /// </summary>
    public sealed class UnityConsoleSink : ILogSink
    {
        // El rich text (<color=...>) lo interpreta la consola del editor.
        // En el log de un player se veria el tag literal, asi que solo se usa en editor.
#if UNITY_EDITOR
        private const bool RichTextSupported = true;
#else
        private const bool RichTextSupported = false;
#endif

        private readonly LogSettings _settings;
        private readonly StringBuilder _builder = new StringBuilder(256);

        public UnityConsoleSink(LogSettings settings)
        {
            _settings = settings;
        }

        // [HideInCallstack] saca este metodo del stack trace de la consola,
        // para que el doble click apunte a quien llamo al log y no aqui.
        [HideInCallstack]
        public void Write(in LogEntry entry)
        {
            string text = Format(entry);
            var context = entry.Context as UnityObject;

            switch (entry.Level)
            {
                case LogLevel.Error:
                    Debug.LogError(text, context);
                    break;
                case LogLevel.Warning:
                    Debug.LogWarning(text, context);
                    break;
                default:
                    Debug.Log(text, context);
                    break;
            }
        }

        private string Format(in LogEntry entry)
        {
            _builder.Clear();

            if (_settings != null && _settings.ShowFrame)
            {
                _builder.Append("[f:").Append(Time.frameCount).Append("] ");
            }

            bool color = RichTextSupported && _settings != null && _settings.UseColor;
            if (color)
            {
                _builder.Append("<color=#")
                        .Append(ColorUtility.ToHtmlStringRGB(_settings.GetChannelColor(entry.Channel)))
                        .Append(">[").Append(entry.Channel).Append("]</color> ");
            }
            else
            {
                _builder.Append('[').Append(entry.Channel).Append("] ");
            }

            _builder.Append(entry.Message);
            return _builder.ToString();
        }
    }
}
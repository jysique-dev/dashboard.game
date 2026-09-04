using System;
using UnityEngine;
using UnityEngine.Events;

namespace LoopEngine.ColorEngine
{
    [Serializable]
    public class ColorUnityEvent : UnityEvent<Color> { }

    /// <summary>
    /// Escape hatch generico: resuelve un rol de color y lo emite por UnityEvent.
    /// Sirve para conectar la paleta a CUALQUIER consumidor sin acoplarlo al modulo
    /// (Light.color, Image.color, un setter propio de GridCellHighlighter, un shader, etc.)
    /// desde el Inspector.
    ///
    /// Se reemite automaticamente cuando la paleta cambia.
    /// </summary>
    [ExecuteAlways]
    [AddComponentMenu("LoopEngine/Color/Palette Color Event")]
    public sealed class PaletteColorEvent : MonoBehaviour
    {
        [SerializeField] private PaletteColorReference color = new PaletteColorReference();
        [SerializeField] private ColorUnityEvent onColorResolved = new ColorUnityEvent();

        private ColorPalette subscribedPalette;

        public Color CurrentColor => color.Value;
        public ColorUnityEvent OnColorResolved => onColorResolved;

        private void OnEnable()
        {
            Subscribe();
            Emit();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void OnValidate()
        {
            if (!isActiveAndEnabled) return;
            Subscribe();
            Emit();
        }

        public void Emit()
        {
            onColorResolved?.Invoke(color.Value);
        }

        private void Subscribe()
        {
            ColorPalette palette = color?.Palette;
            if (subscribedPalette == palette) return;

            Unsubscribe();
            subscribedPalette = palette;
            if (subscribedPalette != null)
                subscribedPalette.Changed += Emit;
        }

        private void Unsubscribe()
        {
            if (subscribedPalette == null) return;
            subscribedPalette.Changed -= Emit;
            subscribedPalette = null;
        }
    }
}
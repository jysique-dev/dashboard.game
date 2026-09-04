using UnityEngine;

namespace LoopEngine.ColorEngine
{
    /// <summary>
    /// Aplica un rol de color a uno o varios Renderers usando MaterialPropertyBlock,
    /// sin instanciar materiales (el mismo patron que ya usa GridCellHighlighter).
    ///
    /// [ExecuteAlways] para ver el color en la escena sin entrar en Play.
    /// </summary>
    [ExecuteAlways]
    [AddComponentMenu("LoopEngine/Color/Renderer Palette Color")]
    public sealed class RendererPaletteColor : MonoBehaviour
    {
        [SerializeField] private Renderer[] targets = new Renderer[0];

        [Tooltip("Propiedad de color del shader. URP/Lit: _BaseColor. Built-in/Unlit: _Color.")]
        [SerializeField] private string shaderProperty = "_BaseColor";

        [SerializeField] private PaletteColorReference color = new PaletteColorReference();

        private MaterialPropertyBlock block;
        private ColorPalette subscribedPalette;
        private int cachedPropertyId;
        private string cachedPropertyName;

        public Color CurrentColor => color.Value;

        private void Reset()
        {
            targets = GetComponentsInChildren<Renderer>(true);
        }

        private void OnEnable()
        {
            Subscribe();
            Apply();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void OnValidate()
        {
            if (!isActiveAndEnabled) return;
            Subscribe();
            Apply();
        }

        /// <summary>Reaplica el color. Publico para llamarlo tras cambiar targets en runtime.</summary>
        public void Apply()
        {
            if (targets == null || targets.Length == 0) return;
            if (string.IsNullOrEmpty(shaderProperty)) return;

            if (cachedPropertyName != shaderProperty)
            {
                cachedPropertyName = shaderProperty;
                cachedPropertyId = Shader.PropertyToID(shaderProperty);
            }

            block ??= new MaterialPropertyBlock();
            Color value = color.Value;

            for (int i = 0; i < targets.Length; i++)
            {
                Renderer renderer = targets[i];
                if (renderer == null) continue;

                // Leer primero para no borrar overrides previos del mismo renderer.
                renderer.GetPropertyBlock(block);
                block.SetColor(cachedPropertyId, value);
                renderer.SetPropertyBlock(block);
            }
        }

        private void Subscribe()
        {
            ColorPalette palette = color?.Palette;
            if (subscribedPalette == palette) return;

            Unsubscribe();
            subscribedPalette = palette;
            if (subscribedPalette != null)
                subscribedPalette.Changed += Apply;
        }

        private void Unsubscribe()
        {
            if (subscribedPalette == null) return;
            subscribedPalette.Changed -= Apply;
            subscribedPalette = null;
        }
    }
}
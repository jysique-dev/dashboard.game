using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace LoopEngine.IsoCamera.Occlusion
{
    /// <summary>
    /// Oculta el renderer por completo. Cero configuración y cero dependencias de
    /// pipeline: funciona igual en Built-in, URP y HDRP. Visualmente es brusco,
    /// pero es la única que no puede fallar. Úsala como referencia para comprobar
    /// que la detección funciona antes de pelearte con materiales.
    /// </summary>
    public sealed class HideFadeStrategy : IOccluderFadeStrategy
    {
        private readonly HashSet<Renderer> hidden = new HashSet<Renderer>();

        public void Apply(Renderer renderer, float alpha)
        {
            if (renderer == null || !hidden.Add(renderer)) return;
            renderer.enabled = false;
        }

        public void Restore(Renderer renderer)
        {
            if (renderer == null || !hidden.Remove(renderer)) return;
            renderer.enabled = true;
        }

        public void Dispose()
        {
            foreach (var r in hidden) if (r != null) r.enabled = true;
            hidden.Clear();
        }
    }

    /// <summary>
    /// Sustituye los materiales por instancias de un material de fade que tú
    /// configuras en el Inspector (Surface Type = Transparent, o un Shader Graph
    /// con dithering).
    ///
    /// Es la estrategia recomendada: toda la configuración de pipeline la haces una
    /// vez en un asset, y en runtime solo se asignan materiales. No manipula keywords
    /// ni render queues, que es donde falla el cambio de opaco a transparente en caliente.
    ///
    /// Copia _BaseMap y _BaseColor del material original para que el objeto siga
    /// reconociéndose mientras está atenuado.
    /// </summary>
    public sealed class MaterialSwapFadeStrategy : IOccluderFadeStrategy
    {
        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private readonly Material template;
        private readonly Dictionary<Renderer, Material[]> originals = new Dictionary<Renderer, Material[]>();
        private readonly List<Material> instances = new List<Material>();

        public MaterialSwapFadeStrategy(Material template) => this.template = template;

        public void Apply(Renderer renderer, float alpha)
        {
            if (renderer == null || template == null || originals.ContainsKey(renderer)) return;

            Material[] source = renderer.sharedMaterials;
            originals[renderer] = source;

            var faded = new Material[source.Length];
            for (int i = 0; i < source.Length; i++)
            {
                var m = new Material(template) { hideFlags = HideFlags.HideAndDontSave };
                CopyLook(source[i], m, alpha);
                instances.Add(m);
                faded[i] = m;
            }
            renderer.sharedMaterials = faded;
        }

        public void Restore(Renderer renderer)
        {
            if (renderer == null || !originals.TryGetValue(renderer, out var source)) return;
            renderer.sharedMaterials = source;
            originals.Remove(renderer);
        }

        public void Dispose()
        {
            foreach (var kv in originals)
                if (kv.Key != null) kv.Key.sharedMaterials = kv.Value;
            originals.Clear();

            foreach (var m in instances)
                if (m != null) Object.Destroy(m);
            instances.Clear();
        }

        private static void CopyLook(Material from, Material to, float alpha)
        {
            if (from == null) return;

            // Textura base: URP usa _BaseMap, Built-in usa _MainTex.
            if (from.HasProperty(BaseMapId) && to.HasProperty(BaseMapId))
                to.SetTexture(BaseMapId, from.GetTexture(BaseMapId));
            else if (from.HasProperty(MainTexId) && to.HasProperty(MainTexId))
                to.SetTexture(MainTexId, from.GetTexture(MainTexId));

            Color tint = Color.white;
            if (from.HasProperty(BaseColorId)) tint = from.GetColor(BaseColorId);
            else if (from.HasProperty(ColorId)) tint = from.GetColor(ColorId);
            tint.a = alpha;

            if (to.HasProperty(BaseColorId)) to.SetColor(BaseColorId, tint);
            if (to.HasProperty(ColorId)) to.SetColor(ColorId, tint);
        }
    }

    /// <summary>
    /// Convierte el material original a transparente en caliente, sin material de fade.
    ///
    /// ADVERTENCIA: frágil por diseño. Cambiar el Surface Type del Lit de URP desde
    /// código exige tocar keywords, blend modes y render queue a la vez, y los nombres
    /// de esas propiedades han variado entre versiones. Hay informes recurrentes de que
    /// el cambio no surte efecto hasta tocar el material en el Inspector. Se incluye
    /// porque no requiere assets, pero si te falla usa MaterialSwap.
    /// </summary>
    public sealed class RuntimeTransparencyFadeStrategy : IOccluderFadeStrategy
    {
        private static readonly int SurfaceId = Shader.PropertyToID("_Surface");
        private static readonly int SrcBlendId = Shader.PropertyToID("_SrcBlend");
        private static readonly int DstBlendId = Shader.PropertyToID("_DstBlend");
        private static readonly int ZWriteId = Shader.PropertyToID("_ZWrite");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private readonly Dictionary<Renderer, Material[]> originals = new Dictionary<Renderer, Material[]>();
        private readonly List<Material> instances = new List<Material>();

        public void Apply(Renderer renderer, float alpha)
        {
            if (renderer == null || originals.ContainsKey(renderer)) return;

            Material[] source = renderer.sharedMaterials;
            originals[renderer] = source;

            var faded = new Material[source.Length];
            for (int i = 0; i < source.Length; i++)
            {
                if (source[i] == null) { faded[i] = null; continue; }
                var m = new Material(source[i]) { hideFlags = HideFlags.HideAndDontSave };
                MakeTransparent(m, alpha);
                instances.Add(m);
                faded[i] = m;
            }
            renderer.sharedMaterials = faded;
        }

        public void Restore(Renderer renderer)
        {
            if (renderer == null || !originals.TryGetValue(renderer, out var source)) return;
            renderer.sharedMaterials = source;
            originals.Remove(renderer);
        }

        public void Dispose()
        {
            foreach (var kv in originals)
                if (kv.Key != null) kv.Key.sharedMaterials = kv.Value;
            originals.Clear();

            foreach (var m in instances)
                if (m != null) Object.Destroy(m);
            instances.Clear();
        }

        private static void MakeTransparent(Material m, float alpha)
        {
            if (m.HasProperty(SurfaceId)) m.SetFloat(SurfaceId, 1f); // 1 = Transparent en URP Lit
            m.SetOverrideTag("RenderType", "Transparent");

            if (m.HasProperty(SrcBlendId)) m.SetInt(SrcBlendId, (int)BlendMode.SrcAlpha);
            if (m.HasProperty(DstBlendId)) m.SetInt(DstBlendId, (int)BlendMode.OneMinusSrcAlpha);
            if (m.HasProperty(ZWriteId)) m.SetInt(ZWriteId, 0);

            m.DisableKeyword("_ALPHATEST_ON");
            m.EnableKeyword("_ALPHABLEND_ON");
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            m.renderQueue = (int)RenderQueue.Transparent;

            SetAlpha(m, alpha);
        }

        private static void SetAlpha(Material m, float alpha)
        {
            if (m.HasProperty(BaseColorId))
            {
                Color c = m.GetColor(BaseColorId); c.a = alpha; m.SetColor(BaseColorId, c);
            }
            if (m.HasProperty(ColorId))
            {
                Color c = m.GetColor(ColorId); c.a = alpha; m.SetColor(ColorId, c);
            }
        }
    }
}
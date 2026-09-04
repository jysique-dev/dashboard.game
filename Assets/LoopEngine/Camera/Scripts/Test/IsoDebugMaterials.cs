using UnityEngine;
using UnityEngine.Rendering;

namespace LoopEngine.IsoCamera.Test
{
    /// <summary>
    /// Crea materiales de debug válidos en Built-in, URP y HDRP.
    ///
    /// Por qué existe: cada pipeline tiene su propio shader lit y son incompatibles
    /// entre sí. Pedir "Standard" en un proyecto URP devuelve null y Unity cae al
    /// error shader, que es el magenta que todos conocemos.
    ///
    /// Solo se usa en escenas de test; el rig de cámara no depende de esto.
    /// </summary>
    public static class IsoDebugMaterials
    {
        // Nombres de propiedad de color: Built-in Standard usa _Color, URP/HDRP usan _BaseColor.
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private static Shader cachedShader;

        /// <summary>Shader lit compatible con el pipeline activo.</summary>
        public static Shader LitShader
        {
            get
            {
                if (cachedShader != null) return cachedShader;
                cachedShader = FindFirstAvailable(CandidateNames());
                if (cachedShader == null)
                    Debug.LogError("[IsoCamera] No se encontró ningún shader compatible. " +
                                   "Los objetos de test se verán en magenta.");
                return cachedShader;
            }
        }

        /// <summary>Nombre legible del pipeline activo. Para mostrarlo en el HUD.</summary>
        public static string ActivePipelineName
        {
            get
            {
                var rp = GraphicsSettings.currentRenderPipeline;
                if (rp == null) return "Built-in";
                string n = rp.GetType().Name;
                if (n.Contains("Universal")) return "URP";
                if (n.Contains("HD")) return "HDRP";
                return n;
            }
        }

        public static Material Create(Color color)
        {
            Shader s = LitShader;
            var m = new Material(s != null ? s : Shader.Find("Hidden/InternalErrorShader"))
            {
                hideFlags = HideFlags.DontSave
            };
            SetColor(m, color);
            return m;
        }

        /// <summary>
        /// Escribe el color en la propiedad que exista. No basta con material.color:
        /// eso depende de que el shader marque su color principal con [MainColor],
        /// y no todos los shaders de fallback lo hacen.
        /// </summary>
        public static void SetColor(Material m, Color color)
        {
            if (m == null) return;
            if (m.HasProperty(BaseColorId)) m.SetColor(BaseColorId, color);
            if (m.HasProperty(ColorId)) m.SetColor(ColorId, color);
        }

        private static string[] CandidateNames()
        {
            var rp = GraphicsSettings.currentRenderPipeline;

            // Sin pipeline asset asignado => Built-in Render Pipeline.
            if (rp == null)
                return new[] { "Standard", "Legacy Shaders/Diffuse", "Unlit/Color" };

            string pipeline = rp.GetType().Name;

            if (pipeline.Contains("Universal") || pipeline.Contains("URP"))
                return new[]
                {
                    "Universal Render Pipeline/Lit",
                    "Universal Render Pipeline/Simple Lit",
                    "Universal Render Pipeline/Unlit",
                    "Unlit/Color"
                };

            if (pipeline.Contains("HD"))
                return new[] { "HDRP/Lit", "HDRP/Unlit", "Unlit/Color" };

            Debug.LogWarning($"[IsoCamera] Pipeline desconocido '{pipeline}'. Probando shaders genéricos.");
            return new[] { "Universal Render Pipeline/Lit", "Standard", "Unlit/Color" };
        }

        /// <summary>
        /// Nota: no se usa el operador ?? con Shader. Shader hereda de UnityEngine.Object,
        /// que sobrecarga ==; el operador de coalescencia nula se salta esa sobrecarga y
        /// puede dar falsos positivos. La comparación explícita con null sí la respeta.
        /// </summary>
        private static Shader FindFirstAvailable(string[] names)
        {
            for (int i = 0; i < names.Length; i++)
            {
                Shader s = Shader.Find(names[i]);
                if (s != null) return s;
            }
            return null;
        }
    }
}
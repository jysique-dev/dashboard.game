using UnityEngine;

namespace LoopEngine.TagInput
{
    /// <summary>
    /// Overlay de depuración: dibuja en pantalla todos los tags registrados y su estado.
    /// Componente opcional; el manager funciona perfectamente sin él.
    ///
    /// Igual que el manager, toda su lógica está bajo "#if UNITY_EDITOR &amp;&amp; INPUT_TEST".
    /// Usa IMGUI (OnGUI), que genera basura por frame. Es aceptable para una herramienta
    /// de editor, pero por eso no debe existir fuera de él.
    /// </summary>
    [AddComponentMenu("Tag Input/Key Input Debug Overlay")]
    [DisallowMultipleComponent]
    public class KeyInputDebugOverlay : MonoBehaviour
    {
        public enum Corner
        {
            TopLeft,
            TopRight,
            BottomLeft,
            BottomRight
        }

        [Tooltip("Tecla que muestra u oculta el overlay. Es un KeyCode directo, no un tag: " +
                 "si dependiera del manager, un mapa roto dejaría el overlay inaccesible.")]
        [SerializeField]
        private KeyCode toggleKey = KeyCode.F1;

        [Tooltip("El overlay arranca visible.")]
        [SerializeField]
        private bool visibleOnStart = true;

        [Tooltip("Esquina de la pantalla donde se ancla el panel.")]
        [SerializeField]
        private Corner corner = Corner.TopLeft;

        [Tooltip("Ancho del panel en píxeles.")]
        [SerializeField]
        private float panelWidth = 260f;

        [Tooltip("Margen respecto al borde de la pantalla, en píxeles.")]
        [SerializeField]
        private float margin = 10f;

#if UNITY_EDITOR && INPUT_TEST
        private bool visible;
        private GUIStyle rowStyle;
        private GUIStyle headerStyle;
        private string lastPressedTag = "—";
#endif

        private void Start()
        {
#if UNITY_EDITOR && INPUT_TEST
            visible = visibleOnStart;
#endif
        }

        private void Update()
        {
#if UNITY_EDITOR && INPUT_TEST
#if ENABLE_LEGACY_INPUT_MANAGER
            if (toggleKey != KeyCode.None && Input.GetKeyDown(toggleKey))
            {
                visible = !visible;
            }
#endif
            TrackLastPressed();
#endif
        }

#if UNITY_EDITOR && INPUT_TEST
        private void TrackLastPressed()
        {
            KeyInputManager manager = KeyInputManager.Instance;
            if (manager == null)
            {
                return;
            }

            var tags = manager.GetAllTags();
            for (int i = 0; i < tags.Count; i++)
            {
                if (manager.GetKeyDown(tags[i]))
                {
                    lastPressedTag = tags[i];
                    return;
                }
            }
        }

        private void OnGUI()
        {
            if (!visible)
            {
                return;
            }

            EnsureStyles();

            KeyInputManager manager = KeyInputManager.Instance;
            var tags = manager != null ? manager.GetAllTags() : null;

            int rows = tags != null ? tags.Count : 1;
            float height = 54f + rows * 18f;

            Rect area = BuildRect(height);

            GUI.Box(area, GUIContent.none);
            GUILayout.BeginArea(new Rect(area.x + 8f, area.y + 6f, area.width - 16f, area.height - 12f));

            GUILayout.Label("Tag Input — overlay", headerStyle);

            if (manager == null)
            {
                GUILayout.Label("No hay KeyInputManager activo en la escena.", rowStyle);
            }
            else
            {
                GUILayout.Label($"Último pulsado: {lastPressedTag}", rowStyle);

                for (int i = 0; i < tags.Count; i++)
                {
                    string tag = tags[i];
                    manager.TryGetKeyCode(tag, out KeyCode code);
                    bool held = manager.GetKey(tag);

                    Color previous = GUI.color;
                    GUI.color = held ? Color.green : previous;
                    GUILayout.Label($"{(held ? "▣" : "▢")}  {tag}  →  {code}", rowStyle);
                    GUI.color = previous;
                }
            }

            GUILayout.EndArea();
        }

        private Rect BuildRect(float height)
        {
            float x = corner == Corner.TopLeft || corner == Corner.BottomLeft
                ? margin
                : Screen.width - panelWidth - margin;

            float y = corner == Corner.TopLeft || corner == Corner.TopRight
                ? margin
                : Screen.height - height - margin;

            return new Rect(x, y, panelWidth, height);
        }

        private void EnsureStyles()
        {
            if (rowStyle != null)
            {
                return;
            }

            rowStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                padding = new RectOffset(0, 0, 1, 1)
            };

            headerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold
            };
        }
#endif
    }
}
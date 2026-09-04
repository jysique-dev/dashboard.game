using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace LoopEngine.ColorEngine.EditorTools
{
    /// <summary>
    /// Ventana para crear y editar paletas de color.
    /// Menu: Tools > LoopEngine > Color Palette.
    ///
    /// La paleta abierta aqui se guarda como "paleta activa" en EditorPrefs, y el
    /// desplegable del Inspector (PaletteColorReferenceDrawer) la usa como default.
    /// </summary>
    public sealed class ColorPaletteWindow : EditorWindow
    {
        private const string ActivePaletteGuidKey = "LoopEngine.ColorEngine.ActivePaletteGuid";
        private const float SwatchWidth = 60f;
        private const float ButtonWidth = 22f;

        private ColorPalette palette;
        private SerializedObject serializedPalette;
        private SerializedProperty rolesProperty;
        private Vector2 scroll;
        private string filter = string.Empty;
            
        [MenuItem(LoopRoutes.ColorToolRoute + "/Color Palette")]
        public static void Open()
        {
            ColorPaletteWindow window = GetWindow<ColorPaletteWindow>();
            window.titleContent = new GUIContent("Color Palette");
            window.minSize = new Vector2(340f, 260f);
            window.Show();
        }

        /// <summary>Paleta que el drawer del Inspector usa por defecto.</summary>
        public static ColorPalette ActivePalette
        {
            get
            {
                string guid = EditorPrefs.GetString(ActivePaletteGuidKey, string.Empty);
                if (string.IsNullOrEmpty(guid)) return null;

                string path = AssetDatabase.GUIDToAssetPath(guid);
                return string.IsNullOrEmpty(path)
                    ? null
                    : AssetDatabase.LoadAssetAtPath<ColorPalette>(path);
            }
            set
            {
                if (value == null)
                {
                    EditorPrefs.DeleteKey(ActivePaletteGuidKey);
                    return;
                }

                string path = AssetDatabase.GetAssetPath(value);
                EditorPrefs.SetString(ActivePaletteGuidKey, AssetDatabase.AssetPathToGUID(path));
            }
        }

        private void OnEnable()
        {
            palette = ActivePalette;
            Rebind();
        }

        private void OnGUI()
        {
            DrawHeader();

            if (palette == null)
            {
                EditorGUILayout.HelpBox(
                    "Selecciona una paleta o crea una nueva.\n" +
                    "Tambien puedes crearla con: Assets > Create > LoopEngine > Color > Color Palette.",
                    MessageType.Info);

                if (GUILayout.Button("Crear paleta nueva...", GUILayout.Height(24f)))
                    CreatePaletteAsset();

                return;
            }

            if (serializedPalette == null) Rebind();
            serializedPalette.Update();

            DrawToolbar();
            DrawRoles();
            DrawDuplicateWarnings();

            serializedPalette.ApplyModifiedProperties();
        }

        private void DrawHeader()
        {
            EditorGUI.BeginChangeCheck();
            ColorPalette picked = (ColorPalette)EditorGUILayout.ObjectField(
                "Paleta", palette, typeof(ColorPalette), false);

            if (EditorGUI.EndChangeCheck())
            {
                palette = picked;
                ActivePalette = picked;
                Rebind();
            }
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                filter = GUILayout.TextField(filter, EditorStyles.toolbarSearchField);

                if (GUILayout.Button("+ Rol", EditorStyles.toolbarButton, GUILayout.Width(60f)))
                    AddRole();

                if (GUILayout.Button("Ping", EditorStyles.toolbarButton, GUILayout.Width(44f)))
                    EditorGUIUtility.PingObject(palette);
            }
        }

        private void DrawRoles()
        {
            EditorGUI.BeginChangeCheck();
            scroll = EditorGUILayout.BeginScrollView(scroll);

            int removeIndex = -1;
            int moveFrom = -1;
            int moveTo = -1;

            for (int i = 0; i < rolesProperty.arraySize; i++)
            {
                SerializedProperty element = rolesProperty.GetArrayElementAtIndex(i);
                SerializedProperty idProperty = element.FindPropertyRelative("id");
                SerializedProperty colorProperty = element.FindPropertyRelative("color");

                if (!string.IsNullOrEmpty(filter) &&
                    idProperty.stringValue.IndexOf(filter, System.StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                using (new EditorGUILayout.HorizontalScope())
                {
                    colorProperty.colorValue = EditorGUILayout.ColorField(
                        GUIContent.none, colorProperty.colorValue, false, true, false,
                        GUILayout.Width(SwatchWidth));

                    idProperty.stringValue = EditorGUILayout.DelayedTextField(idProperty.stringValue);

                    using (new EditorGUI.DisabledScope(i == 0))
                    {
                        if (GUILayout.Button("\u25B2", GUILayout.Width(ButtonWidth)))
                        {
                            moveFrom = i;
                            moveTo = i - 1;
                        }
                    }

                    using (new EditorGUI.DisabledScope(i == rolesProperty.arraySize - 1))
                    {
                        if (GUILayout.Button("\u25BC", GUILayout.Width(ButtonWidth)))
                        {
                            moveFrom = i;
                            moveTo = i + 1;
                        }
                    }

                    if (GUILayout.Button("X", GUILayout.Width(ButtonWidth)))
                        removeIndex = i;
                }
            }

            EditorGUILayout.EndScrollView();

            if (EditorGUI.EndChangeCheck())
                MarkChanged();

            if (moveFrom >= 0)
                rolesProperty.MoveArrayElement(moveFrom, moveTo);

            if (removeIndex >= 0)
                rolesProperty.DeleteArrayElementAtIndex(removeIndex);

            if (moveFrom >= 0 || removeIndex >= 0)
                MarkChanged();
        }

        private void DrawDuplicateWarnings()
        {
            var seen = new HashSet<string>();
            var duplicates = new List<string>();
            bool hasEmpty = false;

            for (int i = 0; i < rolesProperty.arraySize; i++)
            {
                string id = rolesProperty.GetArrayElementAtIndex(i)
                    .FindPropertyRelative("id").stringValue;

                if (string.IsNullOrWhiteSpace(id))
                {
                    hasEmpty = true;
                    continue;
                }

                if (!seen.Add(id) && !duplicates.Contains(id))
                    duplicates.Add(id);
            }

            if (hasEmpty)
                EditorGUILayout.HelpBox("Hay roles sin id: se ignoran al resolver colores.", MessageType.Warning);

            if (duplicates.Count > 0)
            {
                EditorGUILayout.HelpBox(
                    "Ids repetidos (gana el primero): " + string.Join(", ", duplicates),
                    MessageType.Warning);
            }

            EditorGUILayout.HelpBox(
                "Tip: usa \"/\" en el id (ej. Grid/Hover) para agrupar los roles en submenus.",
                MessageType.None);
        }

        private void AddRole()
        {
            int index = rolesProperty.arraySize;
            rolesProperty.InsertArrayElementAtIndex(index);

            SerializedProperty element = rolesProperty.GetArrayElementAtIndex(index);
            element.FindPropertyRelative("id").stringValue = MakeUniqueId("NewRole");
            element.FindPropertyRelative("color").colorValue = Color.white;

            MarkChanged();
        }

        private string MakeUniqueId(string baseId)
        {
            var existing = new HashSet<string>();
            for (int i = 0; i < rolesProperty.arraySize; i++)
            {
                existing.Add(rolesProperty.GetArrayElementAtIndex(i)
                    .FindPropertyRelative("id").stringValue);
            }

            if (!existing.Contains(baseId)) return baseId;

            int suffix = 1;
            while (existing.Contains(baseId + suffix)) suffix++;
            return baseId + suffix;
        }

        private void CreatePaletteAsset()
        {
            string path = EditorUtility.SaveFilePanelInProject(
                "Crear Color Palette", "ColorPalette", "asset",
                "Elige donde guardar la paleta");

            if (string.IsNullOrEmpty(path)) return;

            ColorPalette asset = CreateInstance<ColorPalette>();
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();

            palette = asset;
            ActivePalette = asset;
            Rebind();
        }

        private void Rebind()
        {
            serializedPalette = palette != null ? new SerializedObject(palette) : null;
            rolesProperty = serializedPalette?.FindProperty("roles");
        }

        private void MarkChanged()
        {
            // ApplyModifiedProperties ya registra Undo y marca el asset como dirty.
            serializedPalette.ApplyModifiedProperties();
            palette.Invalidate();
            SceneView.RepaintAll();
        }
    }
}
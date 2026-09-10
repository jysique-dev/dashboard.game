using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace LoopEngine.GridEngine.EditorTool
{
    /// <summary>
    /// Adds the Import / Save Settings buttons to GridSystem.
    ///
    /// Lives in a folder named Editor, which is how Unity keeps editor-only code out
    /// of builds. Do not move it into Runtime: UnityEditor does not exist in a player.
    /// </summary>
    [CustomEditor(typeof(GridSystem))]
    public class GridSystemEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            GridSystem system = (GridSystem)target;
            bool hasAsset = system.Settings != null;

            EditorGUILayout.Space();

            if (!hasAsset)
            {
                EditorGUILayout.HelpBox(
                    "Assign a GridSettings asset to enable Import and Save. Tune the inline " +
                    "values, look at the result in the scene, then save.",
                    MessageType.Info);
            }

            using (new EditorGUI.DisabledScope(!hasAsset))
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Import Settings", GUILayout.Height(24f)))
                    Import(system);

                if (GUILayout.Button("Save Settings", GUILayout.Height(24f)))
                    Save(system);
            }

            EditorGUILayout.LabelField(
                "Import: asset \u2192 inline.   Save: inline \u2192 asset.",
                EditorStyles.miniLabel);
        }

        private void Import(GridSystem system)
        {
            GridSettings asset = system.Settings;
            if (asset == null) return;

            // Import overwrites everything inline, so it asks first.
            bool confirmed = EditorUtility.DisplayDialog(
                "Import Settings",
                $"Overwrite the inline values of '{system.name}' with '{asset.name}'?\n\n" +
                "The current inline layout, sub-grids and weights will be replaced.",
                "Import", "Cancel");

            if (!confirmed) return;

            Undo.RecordObject(system, "Import Grid Settings");

            if (!system.LoadFromSettings()) return;

            EditorUtility.SetDirty(system);
            MarkSceneDirty(system);
            Debug.Log($"[GridSystem] Inline values imported from '{asset.name}'.", system);
        }

        private void Save(GridSystem system)
        {
            GridSettings asset = system.Settings;
            if (asset == null) return;

            // Registered before the write so Ctrl+Z restores the asset's previous values.
            Undo.RecordObject(asset, "Save Grid Settings");

            if (system.SaveToSettings())
                Debug.Log($"[GridSystem] Inline values saved to '{asset.name}'.", asset);
        }

        /// <summary>
        /// Scene objects are not saved by SetDirty alone; the scene itself must be marked.
        /// Skipped in Play mode, where scene changes are not persisted anyway.
        /// </summary>
        private static void MarkSceneDirty(GridSystem system)
        {
            if (Application.isPlaying) return;
            if (PrefabUtility.IsPartOfPrefabAsset(system)) return;

            EditorSceneManager.MarkSceneDirty(system.gameObject.scene);
        }
    }
}
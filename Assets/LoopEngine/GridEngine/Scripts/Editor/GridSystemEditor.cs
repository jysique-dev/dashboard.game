using UnityEditor;
using UnityEngine;

namespace LoopEngine.GridEngine.EditorTools
{
    /// <summary>
    /// Adds the "Save Settings" button to GridSystem.
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

            EditorGUILayout.Space();

            if (system.Settings == null)
            {
                EditorGUILayout.HelpBox(
                    "Assign a GridSettings asset to enable saving. Tune the inline values first, " +
                    "look at the result in the scene, then save.",
                    MessageType.Info);
            }

            using (new EditorGUI.DisabledScope(system.Settings == null))
            {
                if (GUILayout.Button("Save Settings", GUILayout.Height(24f)))
                    Save(system);
            }
        }

        private void Save(GridSystem system)
        {
            GridSettings target = system.Settings;
            if (target == null) return;

            // Registered before the write so Ctrl+Z restores the asset's previous values.
            Undo.RecordObject(target, "Save Grid Settings");

            if (system.SaveToSettings())
                Debug.Log($"[GridSystem] Inline values saved to '{target.name}'.", target);
        }
    }
}
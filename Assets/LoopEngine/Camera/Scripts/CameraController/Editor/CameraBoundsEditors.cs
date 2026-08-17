using UnityEditor;

namespace LoopEngine.CameraEngine.CameraController.EditorScript
{
    /// <summary>
    /// Editor del asset CameraBounds: al SELECCIONAR el asset en el Project,
    /// aparecen los handles de edición en la vista de escena.
    /// </summary>
    [CustomEditor(typeof(CameraBounds))]
    public class CameraBoundsEditor : Editor
    {
        private void OnSceneGUI()
        {
            CameraBoundsHandles.DrawEditable((CameraBounds)target);
        }

        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox(
                "Selecciona este asset y edita el rectángulo con los handles en la " +
                "vista de escena. También puedes editarlo seleccionando el CameraRig.",
                MessageType.Info);
            DrawDefaultInspector();
        }
    }

    /// <summary>
    /// Editor del controlador: al SELECCIONAR el CameraRig en la escena, permite
    /// editar el mismo asset de límites referenciado (si hay uno asignado).
    /// </summary>
    [CustomEditor(typeof(CityCameraController))]
    public class CityCameraControllerEditor : Editor
    {
        private void OnSceneGUI()
        {
            var controller = (CityCameraController)target;
            if (controller.Bounds != null)
                CameraBoundsHandles.DrawEditable(controller.Bounds);
        }
    }
}
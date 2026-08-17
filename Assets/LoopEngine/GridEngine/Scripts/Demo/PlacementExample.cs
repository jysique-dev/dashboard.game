using UnityEngine;
using UnityEngine.InputSystem;
using LoopEngine.GridEngine.Placement;

namespace LoopEngine.GridEngine.Demo
{
    /// <summary>
    /// Ejemplo: cambia la pieza activa del GridPlacementController con las teclas 1, 2, 3.
    /// Muestra cómo una UI o tu InputReader llamarían a SetActivePlaceable.
    /// </summary>
    public class PlacementExample : MonoBehaviour
    {
        [SerializeField] private GridPlacementController controller;
        [SerializeField] private PlaceableDefinition[] palette;

        private void Update()
        {
            if (Keyboard.current == null || controller == null || palette == null) return;

            if (Keyboard.current.digit1Key.wasPressedThisFrame) Select(0);
            if (Keyboard.current.digit2Key.wasPressedThisFrame) Select(1);
            if (Keyboard.current.digit3Key.wasPressedThisFrame) Select(2);
        }

        private void Select(int index)
        {
            if (index < 0 || index >= palette.Length) return;
            controller.SetActivePlaceable(palette[index]);
            Debug.Log($"Pieza activa: {palette[index].id}");
        }
    }
}
using LoopEngine.GridMovement.Samples;
using LoopEngine.IsoCamera.Core;
using UnityEngine;

public class GameplayDemo : MonoBehaviour
{
    public GridMovementDemo demoMovement; // spawner
    public IsometricCameraController isometricCameraController;


    private void Awake()
    {
        demoMovement.OnSpawnAgent += AddTargetToCamera;
    }

    void AddTargetToCamera(GameObject target)
    {
        isometricCameraController.SetInitialTarget(target);
    }
}

using LoopEngine.IsoCamera.Core;
using LoopEngine.IsoCamera.InputAbstraction;
using LoopEngine.IsoCamera.Occlusion;
using UnityEngine;

namespace LoopEngine.IsoCamera.Test
{
    /// <summary>
    /// Panel de debug en pantalla (OnGUI) para verificar el rig sin abrir el Inspector.
    /// Solo para escenas de test.
    /// </summary>
    [AddComponentMenu("Iso Camera/Test/Iso Camera Debug HUD")]
    public sealed class IsoCameraDebugHUD : MonoBehaviour
    {
        [SerializeField] private IsometricCameraRig rig;
        [SerializeField] private IsometricCameraFollow follow;
        [SerializeField] private IsometricCameraZoom zoom;
        [SerializeField] private IsometricCameraRotator rotator;
        [SerializeField] private IsometricCameraPan pan;
        [SerializeField] private IsometricCameraOcclusion occlusion;
        [SerializeField] private IsometricCameraFocus focus;
        [SerializeField] private Transform[] focusTargets;
        [SerializeField] private bool allowSliders = true;

        private GUIStyle labelStyle;
        private float peakScroll;
        private float peakScrollTimer;

        private void Reset() => AutoWire();
        private void Awake() => AutoWire();

        private void AutoWire()
        {
            if (rig == null) rig = FindFirstObjectByType<IsometricCameraRig>();
            if (rig == null) return;
            if (follow == null) follow = rig.GetComponent<IsometricCameraFollow>();
            if (zoom == null) zoom = rig.GetComponent<IsometricCameraZoom>();
            if (rotator == null) rotator = rig.GetComponent<IsometricCameraRotator>();
            if (pan == null) pan = rig.GetComponent<IsometricCameraPan>();
            if (occlusion == null) occlusion = rig.GetComponent<IsometricCameraOcclusion>();
            if (focus == null) focus = rig.GetComponent<IsometricCameraFocus>();
        }

        private void Update()
        {
            // Retiene el pico de scroll unos segundos para poder leerlo con calma.
            float s = Mathf.Abs(IsoInput.Source.Zoom);
            if (s > peakScroll) { peakScroll = s; peakScrollTimer = 3f; }
            else if (peakScrollTimer > 0f)
            {
                peakScrollTimer -= Time.deltaTime;
                if (peakScrollTimer <= 0f) peakScroll = 0f;
            }
        }

        private void OnGUI()
        {
            if (rig == null) return;
            labelStyle ??= new GUIStyle(GUI.skin.label) { fontSize = 13, richText = true };

            float height = 150f
                           + (follow != null ? 50f : 0f)
                           + (zoom != null ? 40f : 0f)
                           + (rotator != null ? 40f : 0f)
                           + (pan != null ? 20f : 0f)
                           + (occlusion != null ? 20f : 0f)
                           + (focus != null && allowSliders ? 30f : 0f)
                           + (allowSliders ? 150f : 0f);

            GUILayout.BeginArea(new Rect(12, 12, 350, height), GUI.skin.box);

            GUILayout.Label("<b>Isometric Camera Rig</b>", labelStyle);
            GUILayout.Label($"Pipeline: {IsoDebugMaterials.ActivePipelineName}", labelStyle);
            GUILayout.Label($"Pitch: {rig.Pitch:F3}°   Yaw: {rig.Yaw:F1}°", labelStyle);
            GUILayout.Label($"Ortho Size: {rig.OrthographicSize:F2}   Ratio: {rig.TileAspectRatio:F3} : 1", labelStyle);
            GUILayout.Label($"Focus: {rig.FocusPoint}", labelStyle);

            Vector2 h = rig.GroundViewHalfExtents;
            GUILayout.Label($"Huella visible: {h.x * 2f:F1} x {h.y * 2f:F1} u", labelStyle);

            if (follow != null)
            {
                string state = follow.TargetInsideDeadZone
                    ? "<color=#40ff60>DENTRO de dead zone</color>"
                    : "<color=#ffd020>EMPUJANDO</color>";
                GUILayout.Label($"<b>Follow</b>  {state}", labelStyle);
            }

            if (zoom != null)
            {
                GUILayout.Label($"<b>Zoom</b>  t = {zoom.Zoom01:F3} -> {zoom.TargetZoom01:F3}" +
                                $"   rango [{zoom.MinSize:F1}, {zoom.MaxSize:F1}]", labelStyle);
            }

            if (rotator != null)
            {
                string rot = rotator.IsRotating ? "<color=#ffd020>girando</color>" : "en reposo";
                GUILayout.Label($"<b>Rotación</b>  paso {rotator.StepAngle:F0}°   " +
                                $"destino {rotator.TargetYaw:F1}°   {rot}", labelStyle);
            }

            if (pan != null)
            {
                string p = pan.IsPanning ? "<color=#ffd020>PANEANDO</color>" : "en reposo";
                GUILayout.Label($"<b>Pan</b>  {p}", labelStyle);
            }

            if (occlusion != null)
            {
                string o = occlusion.OccluderCount > 0
                    ? $"<color=#ff8040>{occlusion.OccluderCount} ocluyente(s)</color>"
                    : "sin oclusión";
                GUILayout.Label($"<b>Oclusión</b>  {o}", labelStyle);
            }

            // Calibración de rueda: una muesca debería marcar un pico cercano a 1.
            string verdict = peakScroll <= 0.001f ? "gira la rueda"
                : peakScroll > 0.5f && peakScroll < 2f ? "<color=#40ff60>OK</color>"
                : "<color=#ff6060>ajusta ScrollScale</color>";
            GUILayout.Label($"Pico de rueda: {peakScroll:F3}  ({verdict})", labelStyle);

            if (allowSliders)
            {
                GUILayout.Space(4);
                GUILayout.Label($"ScrollScale: {IsoInput.ScrollScale:F2}", labelStyle);
                IsoInput.ScrollScale = GUILayout.HorizontalSlider(IsoInput.ScrollScale, 0.05f, 5f);

                GUILayout.Label("Pitch", labelStyle);
                rig.Pitch = GUILayout.HorizontalSlider(rig.Pitch, 5f, 89.9f);

                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Iso real")) rig.ApplyPreset(IsometricAnglePreset.TrueIsometric);
                if (GUILayout.Button("2:1")) rig.ApplyPreset(IsometricAnglePreset.Dimetric2To1);
                if (GUILayout.Button("45°")) rig.ApplyPreset(IsometricAnglePreset.Military45);
                GUILayout.EndHorizontal();

                if (rotator != null)
                {
                    GUILayout.BeginHorizontal();
                    if (GUILayout.Button("< Girar (Q)")) rotator.RotateSteps(-1);
                    if (GUILayout.Button("Girar (E) >")) rotator.RotateSteps(1);
                    GUILayout.EndHorizontal();
                }

                if (focus != null)
                {
                    GUILayout.BeginHorizontal();
                    if (GUILayout.Button("Enfocar punto") && focusTargets != null && focusTargets.Length > 0)
                        focus.FocusOn(focusTargets[Random.Range(0, focusTargets.Length)]);
                    if (GUILayout.Button("Cancelar")) focus.Cancel();
                    GUILayout.EndHorizontal();
                }
            }
            GUILayout.EndArea();
        }
    }
}
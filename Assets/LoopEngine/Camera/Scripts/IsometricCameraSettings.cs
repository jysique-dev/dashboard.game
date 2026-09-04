using LoopEngine.Core;
using System;
using UnityEngine;

namespace LoopEngine.IsoCamera.Core
{
    /// <summary>
    /// Configuración completa de una cámara isométrica, como asset reutilizable.
    ///
    /// Por qué un ScriptableObject y no valores en cada componente: permite compartir
    /// el mismo ajuste entre escenas, cambiarlo sin abrir prefabs, tener varios perfiles
    /// (exploración, combate, mapa) e intercambiarlos en runtime con una línea.
    ///
    /// Los componentes siguen siendo editables a mano; esto los sobrescribe solo
    /// cuando el controlador aplica el asset.
    /// </summary>
    [CreateAssetMenu(fileName = "IsoCameraSettings",menuName = LoopRoutes.CameraRoute + "/Camera Settings")]
    public sealed class IsometricCameraSettings : ScriptableObject
    {
        public enum PresetKind
        {
            /// <summary>ARPG cenital: cerca, gira poco, sigue firme. Diablo, Hades.</summary>
            ActionRpg,
            /// <summary>Táctico por turnos: rotación en 4 pasos, zoom amplio. XCOM, Into the Breach.</summary>
            Tactical,
            /// <summary>Constructor: paneo libre, sin objetivo, mucho zoom. SimCity, Factorio.</summary>
            CityBuilder
        }

        [Serializable]
        public sealed class AngleSettings :ScriptableSetting
        {
            public IsometricAnglePreset preset = IsometricAnglePreset.Dimetric2To1;
            [Range(0.1f, 89.9f)] public float pitch = IsometricAngles.Dimetric2To1Pitch;
            [Range(-180f, 180f)] public float yaw = 45f;
            [Min(0.1f)] public float distance = 40f;
        }

        [Serializable]
        public sealed class ZoomSettings : ScriptableSetting
        {
            [Min(0.1f)] public float minSize = 4f;
            [Min(0.1f)] public float maxSize = 20f;
            public AnimationCurve distribution = AnimationCurve.Linear(0f, 0f, 1f, 1f);
            [Range(0.01f, 0.5f)] public float stepPerNotch = 0.12f;
            [Min(0f)] public float smoothTime = 0.12f;
            [Range(0f, 1f)] public float startZoom = 0.5f;
            public bool zoomToCursor = false;
        }

        [Serializable]
        public sealed class RotationSettings : ScriptableSetting
        {
            [Range(1, 24)] public int stepsPerTurn = 4;
            [Min(0.01f)] public float duration = 0.25f;
            public AnimationCurve ease = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        }

        [Serializable]
        public sealed class FollowSettings: ScriptableSetting
        {

            public Vector3 worldOffset = Vector3.zero;
            public Vector2 deadZoneSize = new Vector2(3f, 2f);
            [Min(0f)] public float smoothTime = 0.18f;
            [Min(0f)] public float maxSpeed = 0f;
            public bool followVertical = false;
            [Min(0f)] public float verticalSmoothTime = 0.6f;
            public bool clampVisibleArea = true;
        }

        [Serializable]
        public sealed class PanSettings : ScriptableSetting
        {
            public bool edgePan = true;
            [Min(1f)] public float edgeThickness = 24f;
            [Min(0f)] public float edgeSpeed = 14f;
            [Min(0.1f)] public float referenceSize = 10f;
            public bool dragPan = true;
            public bool grabWorld = true;
            public bool suspendFollowWhilePanning = true;
        }

        [Serializable]
        public sealed class OcclusionSettings : ScriptableSetting
        {
            public LayerMask occluderLayers = ~0;
            [Min(0f)] public float castRadius = 0.35f;
            [Min(0f)] public float targetPadding = 0.6f;
            [Min(0f)] public float checkInterval = 0.05f;
            public float targetHeightOffset = 1f;
            [Range(0f, 1f)] public float fadeAlpha = 0.28f;
            [Tooltip("Requerido para el modo MaterialSwap del componente de oclusión.")]
            public Material fadeMaterial;
        }

        [SerializeField] private AngleSettings angles = new AngleSettings();
        [SerializeField] private ZoomSettings zoom = new ZoomSettings();
        [SerializeField] private RotationSettings rotation = new RotationSettings();
        [SerializeField] private FollowSettings follow = new FollowSettings();
        [SerializeField] private PanSettings pan = new PanSettings();
        [SerializeField] private OcclusionSettings occlusion = new OcclusionSettings();

        public AngleSettings Angles => angles;
        public ZoomSettings Zoom => zoom;
        public RotationSettings Rotation => rotation;
        public FollowSettings Follow => follow;
        public PanSettings Pan => pan;
        public OcclusionSettings Occlusion => occlusion;

        // ----------------------------------------------------------- Presets

        [ContextMenu("Preset: Action RPG")]
        private void PresetActionRpg() => LoadPreset(PresetKind.ActionRpg);

        [ContextMenu("Preset: Tactical")]
        private void PresetTactical() => LoadPreset(PresetKind.Tactical);

        [ContextMenu("Preset: City Builder")]
        private void PresetCityBuilder() => LoadPreset(PresetKind.CityBuilder);

        /// <summary>Rellena todos los bloques con un ajuste de partida conocido.</summary>
        public void LoadPreset(PresetKind kind)
        {
            switch (kind)
            {
                case PresetKind.ActionRpg:
                    angles.preset = IsometricAnglePreset.Dimetric2To1;
                    angles.pitch = IsometricAngles.Dimetric2To1Pitch;
                    zoom.minSize = 6f; zoom.maxSize = 12f; zoom.startZoom = 0.3f;
                    zoom.zoomToCursor = false;
                    rotation.Enabled = false;
                    follow.Enabled = true; follow.deadZoneSize = new Vector2(1.5f, 1f);
                    follow.smoothTime = 0.12f;
                    pan.Enabled = false;
                    occlusion.Enabled = true; occlusion.castRadius = 0.4f;
                    break;

                case PresetKind.Tactical:
                    angles.preset = IsometricAnglePreset.TrueIsometric;
                    angles.pitch = IsometricAngles.TrueIsometricPitch;
                    zoom.minSize = 5f; zoom.maxSize = 22f; zoom.startZoom = 0.5f;
                    zoom.zoomToCursor = false;
                    rotation.Enabled = true; rotation.stepsPerTurn = 4; rotation.duration = 0.3f;
                    follow.Enabled = true; follow.deadZoneSize = new Vector2(6f, 4f);
                    follow.smoothTime = 0.3f;
                    pan.Enabled = true; pan.edgePan = true; pan.dragPan = true;
                    occlusion.Enabled = true; occlusion.castRadius = 0.5f;
                    break;

                case PresetKind.CityBuilder:
                    angles.preset = IsometricAnglePreset.Military45;
                    angles.pitch = IsometricAngles.Military45Pitch;
                    zoom.minSize = 4f; zoom.maxSize = 60f; zoom.startZoom = 0.6f;
                    zoom.zoomToCursor = true; zoom.smoothTime = 0.08f;
                    rotation.Enabled = true; rotation.stepsPerTurn = 4; rotation.duration = 0.2f;
                    follow.Enabled = false;
                    pan.Enabled = true; pan.edgePan = true; pan.dragPan = true;
                    pan.edgeSpeed = 20f;
                    occlusion.Enabled = false;
                    break;
            }
        }

        private void OnValidate()
        {
            if (zoom.maxSize < zoom.minSize) zoom.maxSize = zoom.minSize;
            if (IsometricAngles.TryResolve(angles.preset, out float p, out float y))
            {
                angles.pitch = p;
                angles.yaw = y;
            }
        }
    }
}
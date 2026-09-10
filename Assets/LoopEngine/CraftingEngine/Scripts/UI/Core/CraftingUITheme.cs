using UnityEngine;

namespace LoopEngine.CraftingEngine.UI
{
    /// <summary>
    /// Every colour and measurement the crafting UI uses, in one object. There is no USS
    /// file anywhere in this module: styles are applied from code, so a project can swap the
    /// whole look by handing a different theme to the <see cref="CraftingUIContext"/>.
    /// </summary>
    public class CraftingUITheme
    {
        // --- Surfaces ---
        public Color Background = new Color(0.10f, 0.11f, 0.13f, 0.96f);
        public Color Surface = new Color(0.15f, 0.16f, 0.19f, 1f);
        public Color SurfaceRaised = new Color(0.20f, 0.21f, 0.25f, 1f);
        public Color Border = new Color(0.28f, 0.29f, 0.34f, 1f);

        // --- Text ---
        public Color TextPrimary = new Color(0.92f, 0.93f, 0.95f, 1f);
        public Color TextMuted = new Color(0.62f, 0.64f, 0.70f, 1f);
        public Color TextDisabled = new Color(0.42f, 0.43f, 0.48f, 1f);

        // --- State ---
        public Color Accent = new Color(0.36f, 0.66f, 0.98f, 1f);
        public Color Running = new Color(0.36f, 0.78f, 0.52f, 1f);
        public Color Blocked = new Color(0.94f, 0.68f, 0.26f, 1f);
        public Color Cancelled = new Color(0.88f, 0.36f, 0.36f, 1f);
        public Color Completed = new Color(0.55f, 0.72f, 0.95f, 1f);
        public Color TrackEmpty = new Color(0.24f, 0.25f, 0.29f, 1f);

        // --- Metrics ---
        public float SpacingTight = 4f;
        public float Spacing = 8f;
        public float SpacingLoose = 16f;
        public float CornerRadius = 6f;
        public float BorderWidth = 1f;
        public float IconSize = 40f;
        public float RowHeight = 52f;
        public float ProgressHeight = 8f;

        // --- Type scale ---
        public int FontTitle = 18;
        public int FontBody = 13;
        public int FontSmall = 11;


        /// <summary>Colour matching a job state, for slot borders and progress fills.</summary>

        public Color GetStateColor(JobState state)
        {
            switch (state)
            {
                case JobState.Running: return Running;
                case JobState.Blocked: return Blocked;
                case JobState.Completed: return Completed;
                case JobState.Cancelled: return Cancelled;
                default:
                    return TrackEmpty;
            }
        }

        public static CraftingUITheme Default => new CraftingUITheme();
    }
}
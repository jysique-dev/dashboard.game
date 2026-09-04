namespace LoopEngine.LogEngine
{
    /// <summary>
    /// Canales conocidos del proyecto. Son <c>const string</c> y no un enum
    /// para que cualquier modulo nuevo pueda inventar su canal sin tocar este archivo
    /// (y para que el asset LogSettings pueda guardarlos como texto sin migraciones).
    /// </summary>
    public static class LogChannels
    {
        public const string General = "General";
        public const string Camera = "Camera";
        public const string Grid = "Grid";
        public const string Placement = "Placement";
        public const string Pathfinding = "Pathfinding";
        public const string Wfc = "WFC";
        public const string Terrain = "Terrain";
        public const string Color = "Color";
        public const string EditorTools = "EditorTools";
    }
}
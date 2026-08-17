namespace LoopEngine.GridEngine
{
    /// <summary>
    /// Plano del mundo sobre el que se proyecta la grilla.
    /// XZ: suelo horizontal (por defecto para el citybuilder 2.5D, con Y hacia arriba).
    /// XY: plano frontal, estilo 2D (coincide con la orientación de la grilla integrada de Unity).
    /// </summary>
    public enum GridPlane
    {
        XZ,
        XY
    }
}
namespace LoopEngine.WfcEngine
{
    /// <summary>
    /// Quien quiera restringir un volumen antes de colapsarlo implementa esto y se cuelga
    /// del mismo GameObject que el WfcRunner. El runner lo busca por GetComponent y no
    /// conoce ninguna implementacion concreta.
    ///
    /// Asi el pintor de restricciones de esta sesion, un generador de reglas por zona, o
    /// la integracion con GridEngine de la sesion 10 son intercambiables sin tocar el runner.
    /// </summary>
    public interface IWfcConstraintSource
    {
        /// <param name="runner">Runner ya preparado: Solver y Adjacency estan disponibles.</param>
        /// <param name="failure">Motivo legible si devuelve false.</param>
        bool ApplyConstraints(WfcRunner runner, out string failure);
    }
}
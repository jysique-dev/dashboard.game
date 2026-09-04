using UnityEngine;

namespace LoopEngine.IsoCamera.Occlusion
{
    /// <summary>
    /// Define QUÉ se le hace a un renderer que tapa al objetivo. El detector solo
    /// decide QUIÉN tapa; separarlo permite cambiar el efecto visual sin tocar la
    /// lógica de raycast, y añadir estrategias propias sin modificar el paquete.
    ///
    /// La estrategia es responsable de recordar el estado original de cada renderer.
    /// </summary>
    public interface IOccluderFadeStrategy
    {
        /// <summary>Se llama una vez cuando el renderer empieza a ocluir.</summary>
        void Apply(Renderer renderer, float alpha);

        /// <summary>Se llama una vez cuando deja de ocluir. Debe dejarlo como estaba.</summary>
        void Restore(Renderer renderer);

        /// <summary>Restaura todo y libera recursos. Se llama al desactivar el componente.</summary>
        void Dispose();
    }
}
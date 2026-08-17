namespace LoopEngine.WfcEngine.Core
{
    /// <summary>
    /// Punto de extension para prohibir vecindades que los sockets permitirian.
    /// El nucleo no sabe de donde salen las reglas; la capa Unity las traduce desde assets.
    ///
    /// Equivale a las reglas de exclusion de marian42: los sockets definen lo que encaja
    /// geometricamente, las exclusiones recortan lo que encaja pero no debe ocurrir.
    /// </summary>
    public interface IWfcAdjacencyFilter
    {
        /// <param name="variantA">Variante en la celda origen.</param>
        /// <param name="variantB">Variante en la celda vecina.</param>
        /// <param name="directionFromAToB">Direccion de A hacia B.</param>
        /// <returns>true si esta vecindad debe prohibirse pese a ser compatible.</returns>
        bool IsForbidden(int variantA, int variantB, WfcDirection directionFromAToB);
    }
}
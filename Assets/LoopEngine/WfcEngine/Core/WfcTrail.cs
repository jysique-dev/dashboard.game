using System;

namespace LoopEngine.WfcEngine.Core
{
    /// <summary>
    /// Diario de deshacer: en vez de copiar el estado entero en cada decision, se anota
    /// el valor anterior de cada palabra antes de modificarla. Retroceder es reponer esas
    /// anotaciones en orden inverso.
    ///
    /// Sustituye a los snapshots de la sesion 6. El coste pasa de O(celdas) por decision a
    /// O(cambios reales), que en propagacion tipica es una fraccion pequena del volumen.
    ///
    /// La correccion depende de una regla que no admite excepciones: toda escritura sobre
    /// domains o collapsed pasa por aqui. Una sola escritura directa deja estado que el
    /// undo no revierte, y eso no lanza excepcion: produce resultados sutilmente mal.
    /// </summary>
    public sealed class WfcTrail
    {
        private int[] wordIndices;
        private ulong[] wordValues;
        private int wordCount;

        private int[] collapsedCells;
        private int collapsedCount;

        public int WordMark => wordCount;
        public int CollapseMark => collapsedCount;

        /// <summary>Maximo de anotaciones vivas. Indicador de cuanta memoria pide el diario.</summary>
        public int PeakWords { get; private set; }

        public WfcTrail(int initialCapacity = 1024)
        {
            if (initialCapacity < 16) initialCapacity = 16;

            wordIndices = new int[initialCapacity];
            wordValues = new ulong[initialCapacity];
            collapsedCells = new int[initialCapacity];
        }

        public void Clear()
        {
            wordCount = 0;
            collapsedCount = 0;
        }

        public void RecordWord(int wordIndex, ulong previousValue)
        {
            if (wordCount == wordIndices.Length) GrowWords();

            wordIndices[wordCount] = wordIndex;
            wordValues[wordCount] = previousValue;
            wordCount++;

            if (wordCount > PeakWords) PeakWords = wordCount;
        }

        public void RecordCollapse(int cell)
        {
            if (collapsedCount == collapsedCells.Length)
            {
                Array.Resize(ref collapsedCells, collapsedCells.Length * 2);
            }

            collapsedCells[collapsedCount++] = cell;
        }

        /// <summary>Deshace hasta las marcas dadas. El orden inverso es obligatorio: una misma
        /// palabra puede haberse escrito varias veces y solo la primera anotacion tiene el
        /// valor original.</summary>
        public void UndoTo(ulong[] domains, int[] collapsed, int wordMark, int collapseMark)
        {
            while (wordCount > wordMark)
            {
                wordCount--;
                domains[wordIndices[wordCount]] = wordValues[wordCount];
            }

            while (collapsedCount > collapseMark)
            {
                collapsedCount--;
                collapsed[collapsedCells[collapsedCount]] = -1;
            }
        }

        private void GrowWords()
        {
            int capacity = wordIndices.Length * 2;
            Array.Resize(ref wordIndices, capacity);
            Array.Resize(ref wordValues, capacity);
        }
    }
}
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace LoopEngine.GridEngine
{
    /// <summary>
    /// Turns a layout + bounds into meshes that can be drawn at runtime.
    /// Vertices are produced in world space, so the meshes are drawn with an
    /// identity matrix. Buffers are reused between rebuilds to avoid garbage.
    /// </summary>
    public class GridMeshBuilder
    {
        private readonly List<Vector3> vertices = new List<Vector3>();
        private readonly List<Color> colors = new List<Color>();
        private readonly List<int> indices = new List<int>();
        private readonly Vector3[] cornerBuffer = new Vector3[4];

        /// <summary>
        /// Fills <paramref name="mesh"/> with the lattice as a line list.
        /// Line topology is one pixel wide on screen regardless of distance;
        /// use quads if you need thickness.
        /// </summary>
        public void BuildLines(IGridLayout layout, GridBounds bounds, Mesh mesh, Color color, float heightOffset = 0f)
        {
            Reset(mesh);
            if (layout == null || mesh == null || bounds.IsEmpty) return;

            Vector3 lift = layout.PlaneNormal * heightOffset;
            GridCoord min = bounds.Min;
            int maxX = min.X + bounds.Width;
            int maxY = min.Y + bounds.Height;

            for (int x = min.X; x <= maxX; x++)
            {
                AddLineVertex(layout.CellToWorld(new GridCoord(x, min.Y)) + lift, color);
                AddLineVertex(layout.CellToWorld(new GridCoord(x, maxY)) + lift, color);
            }

            for (int y = min.Y; y <= maxY; y++)
            {
                AddLineVertex(layout.CellToWorld(new GridCoord(min.X, y)) + lift, color);
                AddLineVertex(layout.CellToWorld(new GridCoord(maxX, y)) + lift, color);
            }

            Apply(mesh, MeshTopology.Lines);
        }

        /// <summary>
        /// Fills <paramref name="mesh"/> with one quad per cell that the colour source
        /// accepts. Colours travel in the vertex stream, so every cell can differ while
        /// still being a single draw call.
        /// </summary>
        public void BuildCells(IGridLayout layout, GridBounds bounds, IGridCellColorSource source, Mesh mesh, float heightOffset = 0f)
        {
            Reset(mesh);
            if (layout == null || source == null || mesh == null || bounds.IsEmpty) return;

            Vector3 lift = layout.PlaneNormal * heightOffset;

            foreach (GridCoord coord in bounds)
            {
                if (!source.TryGetCellColor(coord, out Color color)) continue;
                AddQuad(layout, coord, color, lift);
            }

            Apply(mesh, MeshTopology.Triangles);
        }

        /// <summary>Fills <paramref name="mesh"/> with a quad for each listed cell, all in one colour.</summary>
        public void BuildCells(IGridLayout layout, IReadOnlyList<GridCoord> coords, Color color, Mesh mesh, float heightOffset = 0f)
        {
            Reset(mesh);
            if (layout == null || coords == null || mesh == null) return;

            Vector3 lift = layout.PlaneNormal * heightOffset;

            for (int i = 0; i < coords.Count; i++)
                AddQuad(layout, coords[i], color, lift);

            Apply(mesh, MeshTopology.Triangles);
        }

        // --- Internals ------------------------------------------------------

        private void Reset(Mesh mesh)
        {
            vertices.Clear();
            colors.Clear();
            indices.Clear();
            if (mesh != null) mesh.Clear();
        }

        private void AddLineVertex(Vector3 position, Color color)
        {
            indices.Add(vertices.Count);
            vertices.Add(position);
            colors.Add(color);
        }

        private void AddQuad(IGridLayout layout, GridCoord coord, Color color, Vector3 lift)
        {
            layout.GetCellCorners(coord, cornerBuffer);

            int baseIndex = vertices.Count;
            for (int i = 0; i < 4; i++)
            {
                vertices.Add(cornerBuffer[i] + lift);
                colors.Add(color);
            }

            indices.Add(baseIndex + 0);
            indices.Add(baseIndex + 1);
            indices.Add(baseIndex + 2);
            indices.Add(baseIndex + 0);
            indices.Add(baseIndex + 2);
            indices.Add(baseIndex + 3);
        }

        private void Apply(Mesh mesh, MeshTopology topology)
        {
            if (vertices.Count == 0) return;

            // The 16-bit index buffer tops out at 65535 vertices; large grids need 32-bit.
            mesh.indexFormat = vertices.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16;

            mesh.SetVertices(vertices);
            mesh.SetColors(colors);
            mesh.SetIndices(indices, topology, 0, true);
            mesh.RecalculateBounds();
        }
    }
}
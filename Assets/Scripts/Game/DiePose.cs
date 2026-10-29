using UnityEngine;
using DiceThrower.Core;

namespace DiceThrower.Game
{
    /// <summary>
    /// How a die is turned for its picture (the sprite atlas and the live preview), so that every kind has the classic silhouette:
    /// d4 a triangle, d6 a cube seen from a corner side, d8 a diamond, d10 and d% a kite, d12 a pentagon-faced block, d20 a hexagon.
    /// <see cref="Axes"/> gives the direction that looks at the camera and the direction that points up the picture (the roll is fixed
    /// that way, so the picture never depends on how the model was authored); <see cref="Still"/> adds a small tilt so the die reads as 3D.
    /// </summary>
    public static class DiePose
    {
        /// <summary>The die's rotation for its picture: <see cref="Axes"/>, then tilted by (degrees around x, degrees around y).</summary>
        public static Quaternion Still(DieShape shape, Vector2 tiltDegrees)
        {
            Vector3 view, up;
            Axes(shape, out view, out up);
            // The camera looks along +z, so the die has to show "view" towards -z with "up" towards +y.
            return Quaternion.Euler(tiltDegrees.x, tiltDegrees.y, 0f) * Quaternion.Inverse(Quaternion.LookRotation(-view, up));
        }

        /// <summary>
        /// view: from the die's centre towards the camera. up: a direction that ends up pointing up the picture
        /// (it only needs to be not parallel to view; the part along view is ignored).
        /// </summary>
        public static void Axes(DieShape shape, out Vector3 view, out Vector3 up)
        {
            switch (shape.Kind)
            {
                case DieKind.D4: Tetrahedron(shape, out view, out up); break;
                case DieKind.D8: Octahedron(shape, out view, out up); break;
                case DieKind.D10:
                case DieKind.D10Tens:
                case DieKind.D10Units: Trapezohedron(shape, out view, out up); break;
                case DieKind.D6: FaceOn(shape, true, out view, out up); break;
                default: FaceOn(shape, false, out view, out up); break; // d12, d20
            }
        }

        // ------------------------------------------------------------------ per shape

        /// <summary>d4: the face that holds the highest corner looks at the camera, that corner up: a triangle with its point up.</summary>
        private static void Tetrahedron(DieShape shape, out Vector3 view, out Vector3 up)
        {
            int[] labels = shape.InitialVertexLabels; // null = 1..4 in corner order
            int corners = labels != null ? labels.Length : shape.Verts.Length; // an imported d4 lists more mesh points than its four corners
            int best = 0;
            for (int i = 1; i < corners; i++)
            {
                int value = labels != null ? labels[i] : i + 1;
                int bestValue = labels != null ? labels[best] : best + 1;
                if (value > bestValue) best = i;
            }

            int face = 0;
            for (int f = 0; f < shape.FaceCount; f++)
            {
                if (System.Array.IndexOf(Corners(shape, f), best) >= 0) { face = f; break; }
            }
            view = shape.FaceNormals[face];
            up = shape.Verts[best] - FaceCentre(shape, face);
        }

        /// <summary>d8: seen along a corner with a neighbouring corner up, which makes a square standing on its tip (a diamond).</summary>
        private static void Octahedron(DieShape shape, out Vector3 view, out Vector3 up)
        {
            view = shape.Verts[0];
            int nearest = 1;
            for (int i = 2; i < shape.Verts.Length; i++)
            {
                if ((shape.Verts[i] - view).sqrMagnitude < (shape.Verts[nearest] - view).sqrMagnitude) nearest = i;
            }
            up = shape.Verts[nearest];
        }

        /// <summary>d10 and d%: the pole axis stands upright and a face looks at the camera: a kite with the two poles as its tips.</summary>
        private static void Trapezohedron(DieShape shape, out Vector3 view, out Vector3 up)
        {
            var faceCount = new int[shape.Verts.Length];
            for (int f = 0; f < shape.FaceCount; f++)
            {
                foreach (int corner in Corners(shape, f)) faceCount[corner]++;
            }
            int pole = 0; // the corner that all five upper (or lower) faces share
            for (int i = 1; i < faceCount.Length; i++)
            {
                if (faceCount[i] > faceCount[pole]) pole = i;
            }

            Vector3 axis = shape.Verts[pole].normalized;
            Vector3 centre = shape.FaceCenters[0];
            view = centre - axis * Vector3.Dot(centre, axis); // sideways, towards the first face
            up = axis;
        }

        /// <summary>d6, d12, d20: the face with the highest number looks at the camera. d6: an edge up (a square); others: a corner up (a pentagon, a triangle).</summary>
        private static void FaceOn(DieShape shape, bool edgeUp, out Vector3 view, out Vector3 up)
        {
            int face = HighestFace(shape);
            int[] corners = Corners(shape, face);
            view = shape.FaceNormals[face];
            Vector3 towards = edgeUp ? (shape.Verts[corners[0]] + shape.Verts[corners[1]]) * 0.5f : shape.Verts[corners[0]];
            up = towards - FaceCentre(shape, face);
        }

        // ------------------------------------------------------------------ helpers

        private static int HighestFace(DieShape shape)
        {
            int[] values = shape.InitialFaceValue;
            int best = 0;
            if (values == null) return best;
            for (int f = 1; f < shape.FaceCount && f < values.Length; f++)
            {
                if (values[f] > values[best]) best = f;
            }
            return best;
        }

        /// <summary>The corners of a face. An imported d4 face lists extra mesh points after its three corners; they are left out.</summary>
        private static int[] Corners(DieShape shape, int face)
        {
            int[] all = shape.FaceVerts[face];
            if (!shape.VertexLabeled || all.Length <= 3) return all;
            return new[] { all[0], all[1], all[2] };
        }

        private static Vector3 FaceCentre(DieShape shape, int face)
        {
            int[] corners = Corners(shape, face);
            Vector3 sum = Vector3.zero;
            for (int i = 0; i < corners.Length; i++) sum += shape.Verts[corners[i]];
            return sum / corners.Length;
        }
    }
}

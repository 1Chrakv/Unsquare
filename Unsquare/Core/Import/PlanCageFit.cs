using System;
using Unity.Mathematics;
using Unsquare.Core.Cage;

namespace Unsquare.Core.Import
{
    /// <summary>
    /// Least-squares fit of a plan-bilinear map on a box: finds the 4 deformed plan corners (bit 0 = +x, bit 1 = +z)
    /// whose blend best moves each sample to its target. Used where a single cage can only approximate a shape.
    /// </summary>
    public static class PlanCageFit
    {
        private const int Corners = 4;
        private const double SingularPivot = 1e-9;

        /// <summary>False if the samples don't pin down all 4 corners (too few or all on one line).</summary>
        public static bool TryFit(CageBox box, float2[] from, float2[] to, out float2[] planCorners, out float maxError)
        {
            if (from == null || to == null || from.Length != to.Length)
                throw new ArgumentException("Sample and target arrays must have the same length.");
            planCorners = null;
            maxError = 0f;

            // Normal equations A·c = b, one matrix for both axes; doubles keep the elimination stable.
            var a = new double[Corners, Corners];
            var bx = new double[Corners];
            var bz = new double[Corners];
            var w = new double[Corners];
            for (int s = 0; s < from.Length; s++)
            {
                Weights(box, from[s], w);
                for (int i = 0; i < Corners; i++)
                {
                    for (int j = 0; j < Corners; j++)
                        a[i, j] += w[i] * w[j];
                    bx[i] += w[i] * to[s].x;
                    bz[i] += w[i] * to[s].y;
                }
            }
            if (!TrySolve(a, bx, bz))
                return false;

            planCorners = new float2[Corners];
            for (int i = 0; i < Corners; i++)
                planCorners[i] = new float2((float)bx[i], (float)bz[i]);
            for (int s = 0; s < from.Length; s++)
            {
                Weights(box, from[s], w);
                double x = 0, z = 0;
                for (int i = 0; i < Corners; i++)
                {
                    x += w[i] * bx[i];
                    z += w[i] * bz[i];
                }
                maxError = math.max(maxError, math.distance(new float2((float)x, (float)z), to[s]));
            }
            return true;
        }

        /// <summary>Points along the outline of a plan rectangle, <paramref name="perSide"/> per side, for cage fits.</summary>
        public static float2[] Outline(float2 min, float2 max, int perSide)
        {
            var points = new float2[perSide * 4];
            for (int i = 0; i < perSide; i++)
            {
                float t = i / (float)perSide;
                points[i] = new float2(math.lerp(min.x, max.x, t), min.y);
                points[perSide + i] = new float2(max.x, math.lerp(min.y, max.y, t));
                points[2 * perSide + i] = new float2(math.lerp(max.x, min.x, t), max.y);
                points[3 * perSide + i] = new float2(min.x, math.lerp(max.y, min.y, t));
            }
            return points;
        }

        private static void Weights(CageBox box, float2 p, double[] w)
        {
            double u = (p.x - box.Min.x) / (double)box.Size.x;
            double v = (p.y - box.Min.z) / (double)box.Size.z;
            w[0] = (1 - u) * (1 - v);
            w[1] = u * (1 - v);
            w[2] = (1 - u) * v;
            w[3] = u * v;
        }

        // Gaussian elimination with partial pivoting; solves in place, the solutions end up in bx and bz.
        private static bool TrySolve(double[,] a, double[] bx, double[] bz)
        {
            for (int col = 0; col < Corners; col++)
            {
                int pivot = col;
                for (int row = col + 1; row < Corners; row++)
                {
                    if (Math.Abs(a[row, col]) > Math.Abs(a[pivot, col]))
                        pivot = row;
                }
                if (Math.Abs(a[pivot, col]) < SingularPivot)
                    return false;
                if (pivot != col)
                {
                    for (int k = 0; k < Corners; k++)
                        (a[col, k], a[pivot, k]) = (a[pivot, k], a[col, k]);
                    (bx[col], bx[pivot]) = (bx[pivot], bx[col]);
                    (bz[col], bz[pivot]) = (bz[pivot], bz[col]);
                }
                for (int row = 0; row < Corners; row++)
                {
                    if (row == col)
                        continue;
                    double factor = a[row, col] / a[col, col];
                    for (int k = col; k < Corners; k++)
                        a[row, k] -= factor * a[col, k];
                    bx[row] -= factor * bx[col];
                    bz[row] -= factor * bz[col];
                }
            }
            for (int i = 0; i < Corners; i++)
            {
                bx[i] /= a[i, i];
                bz[i] /= a[i, i];
            }
            return true;
        }
    }
}

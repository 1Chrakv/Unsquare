using Unity.Mathematics;

namespace Unsquare.Core.Deformation
{
    /// <summary>
    /// The game's packed vertex directions: normals as two SNorm16 octahedral coordinates, tangents as two
    /// 15-bit coordinates plus the handedness in the sign bit, both in one 32-bit word.
    /// </summary>
    public static class OctahedralPacking
    {
        private const int Max16 = (1 << 15) - 1;
        private const int Max15 = (1 << 14) - 1;
        private const uint HandednessBit = 0x80000000u;

        public static uint PackNormal(float3 normal)
        {
            float2 e = Encode(normal);
            return (uint)(Quantize(e.x, Max16) & 0xFFFF) | ((uint)(Quantize(e.y, Max16) & 0xFFFF) << 16);
        }

        public static float3 UnpackNormal(uint packed) => Decode(new float2(
            Dequantize(SignExtend(packed & 0xFFFF, 16), Max16),
            Dequantize(SignExtend(packed >> 16 & 0xFFFF, 16), Max16)));

        public static uint PackTangent(float4 tangent)
        {
            float2 e = Encode(tangent.xyz);
            uint packed = (uint)(Quantize(e.x, Max15) & 0x7FFF) | ((uint)(Quantize(e.y, Max15) & 0x7FFF) << 16);
            return tangent.w < 0f ? packed | HandednessBit : packed;
        }

        public static float4 UnpackTangent(uint packed)
        {
            float3 direction = Decode(new float2(
                Dequantize(SignExtend(packed & 0x7FFF, 15), Max15),
                Dequantize(SignExtend(packed >> 16 & 0x7FFF, 15), Max15)));
            return new float4(direction, (packed & HandednessBit) != 0 ? -1f : 1f);
        }

        /// <summary>Maps a direction onto the unit octahedron, folding the lower half into the corners.</summary>
        public static float2 Encode(float3 n)
        {
            float sum = math.abs(n.x) + math.abs(n.y) + math.abs(n.z);
            if (sum == 0f)
                return float2.zero;
            float2 p = n.xy / sum;
            if (n.z >= 0f)
                return p;
            return new float2((1f - math.abs(p.y)) * SignNonZero(p.x), (1f - math.abs(p.x)) * SignNonZero(p.y));
        }

        public static float3 Decode(float2 e)
        {
            float z = 1f - math.abs(e.x) - math.abs(e.y);
            float2 p = z >= 0f
                ? e
                : new float2((1f - math.abs(e.y)) * SignNonZero(e.x), (1f - math.abs(e.x)) * SignNonZero(e.y));
            return math.normalizesafe(new float3(p, z), new float3(0f, 0f, 1f));
        }

        private static float SignNonZero(float v) => v >= 0f ? 1f : -1f;

        private static int Quantize(float v, int max) =>
            (int)(math.clamp(v, -1f, 1f) * max + (v >= 0f ? 0.5f : -0.5f));

        private static float Dequantize(int q, int max) => math.clamp(q / (float)max, -1f, 1f);

        private static int SignExtend(uint value, int bits)
        {
            int shift = 32 - bits;
            return (int)(value << shift) >> shift;
        }
    }
}

namespace MinecraftClone.Utils;

public class SimplexNoise
{
    private readonly int[] _perm = new int[512];

    public SimplexNoise(int seed)
    {
        var rnd = new Random(seed);
        var p = new int[256];
        for (int i = 0; i < 256; i++) p[i] = i;

        for (int i = 255; i > 0; i--)
        {
            int j = rnd.Next(i + 1);
            (p[i], p[j]) = (p[j], p[i]);
        }

        for (int i = 0; i < 512; i++)
            _perm[i] = p[i & 255];
    }

    public float Noise(float xin, float yin)
    {
        const float F2 = 0.3660254037844386f;
        const float G2 = 0.21132486540518713f;

        float s = (xin + yin) * F2;
        int i = FastFloor(xin + s);
        int j = FastFloor(yin + s);

        float t = (i + j) * G2;
        float x0 = xin - (i - t);
        float y0 = yin - (j - t);

        int i1 = x0 > y0 ? 1 : 0;
        int j1 = x0 > y0 ? 0 : 1;

        float x1 = x0 - i1 + G2;
        float y1 = y0 - j1 + G2;

        float x2 = x0 - 1f + 2f * G2;
        float y2 = y0 - 1f + 2f * G2;

        int ii = i & 255;
        int jj = j & 255;

        float n0 = 0, n1 = 0, n2 = 0;

        float t0 = 0.5f - x0 * x0 - y0 * y0;
        if (t0 > 0) { t0 *= t0; n0 = t0 * t0 * Grad(_perm[ii + _perm[jj]], x0, y0); }

        float t1 = 0.5f - x1 * x1 - y1 * y1;
        if (t1 > 0) { t1 *= t1; n1 = t1 * t1 * Grad(_perm[ii + i1 + _perm[jj + j1]], x1, y1); }

        float t2 = 0.5f - x2 * x2 - y2 * y2;
        if (t2 > 0) { t2 *= t2; n2 = t2 * t2 * Grad(_perm[ii + 1 + _perm[jj + 1]], x2, y2); }

        return 70f * (n0 + n1 + n2);
    }

    // Фрактальный шум для реалистичного рельефа
    public float FractalNoise(float x, float y, int octaves = 4, float persistence = 0.5f, float lacunarity = 2.0f)
    {
        float total = 0;
        float frequency = 1;
        float amplitude = 1;
        float maxValue = 0;

        for (int i = 0; i < octaves; i++)
        {
            total += Noise(x * frequency, y * frequency) * amplitude;
            maxValue += amplitude;
            amplitude *= persistence;
            frequency *= lacunarity;
        }

        return total / maxValue;
    }

    // 3D шум для пещер (используем 2D шум с разными смещениями для имитации 3D)
    public float Noise3D(float x, float y, float z)
    {
        return Noise(x + z * 0.5f, y + z * 0.5f) * Noise(y - z * 0.5f, x + z * 0.3f);
    }

    private static int FastFloor(float x) => x > 0 ? (int)x : (int)x - 1;

    private static float Grad(int hash, float x, float y)
    {
        int h = hash & 7;
        float u = h < 4 ? x : y;
        float v = h < 4 ? y : x;
        return ((h & 1) != 0 ? -u : u) + ((h & 2) != 0 ? -2f * v : 2f * v);
    }
}
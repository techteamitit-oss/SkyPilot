namespace SkyPilot.Utils;

/// <summary>
/// Self-contained Fast Fourier Transform implementation.
/// Radix-2 Cooley-Tukey FFT algorithm.
/// </summary>
public static class FftComputer
{
    /// <summary>
    /// Compute FFT of real input data. Returns magnitude spectrum.
    /// </summary>
    public static double[] ComputeMagnitude(double[] input, bool inDecibels = true)
    {
        int n = input.Length;
        if (n < 2 || (n & (n - 1)) != 0)
        {
            // Pad to next power of 2
            n = 1;
            while (n < input.Length) n <<= 1;
        }

        double[] real = new double[n];
        double[] imag = new double[n];

        Array.Copy(input, real, Math.Min(input.Length, n));

        // Apply Hanning window
        for (int i = 0; i < n; i++)
            real[i] *= 0.5 * (1.0 - Math.Cos(2.0 * Math.PI * i / (n - 1)));

        // FFT
        FFT(real, imag, n);

        // Compute magnitude
        int halfN = n / 2;
        double[] magnitude = new double[halfN];
        for (int i = 0; i < halfN; i++)
        {
            double m = Math.Sqrt(real[i] * real[i] + imag[i] * imag[i]) / n;
            magnitude[i] = inDecibels ? 20.0 * Math.Log10(Math.Max(m, 1e-10)) : m;
        }

        return magnitude;
    }

    /// <summary>
    /// Compute frequency bin labels for the FFT output.
    /// </summary>
    public static double[] FrequencyTable(int fftSize, double sampleRate)
    {
        double[] freqs = new double[fftSize / 2];
        double binWidth = sampleRate / fftSize;
        for (int i = 0; i < freqs.Length; i++)
            freqs[i] = i * binWidth;
        return freqs;
    }

    private static void FFT(double[] real, double[] imag, int n)
    {
        // Bit-reversal permutation
        int j = 0;
        for (int i = 0; i < n - 1; i++)
        {
            if (i < j)
            {
                (real[i], real[j]) = (real[j], real[i]);
                (imag[i], imag[j]) = (imag[j], imag[i]);
            }

            int m = n >> 1;
            while (m >= 1 && j >= m)
            {
                j -= m;
                m >>= 1;
            }
            j += m;
        }

        // Cooley-Tukey iterative FFT
        for (int step = 2; step <= n; step <<= 1)
        {
            int halfStep = step >> 1;
            double angle = -2.0 * Math.PI / step;
            double wReal = Math.Cos(angle);
            double wImag = Math.Sin(angle);

            for (int start = 0; start < n; start += step)
            {
                double curReal = 1.0;
                double curImag = 0.0;

                for (int k = 0; k < halfStep; k++)
                {
                    int t = start + k;
                    int u = t + halfStep;

                    double tReal = curReal * real[u] - curImag * imag[u];
                    double tImag = curReal * imag[u] + curImag * real[u];

                    real[u] = real[t] - tReal;
                    imag[u] = imag[t] - tImag;
                    real[t] += tReal;
                    imag[t] += tImag;

                    double newCurReal = curReal * wReal - curImag * wImag;
                    curImag = curReal * wImag + curImag * wReal;
                    curReal = newCurReal;
                }
            }
        }
    }
}

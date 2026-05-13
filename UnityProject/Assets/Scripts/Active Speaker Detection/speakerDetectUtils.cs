using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Utility class for extracting MFCC (Mel-Frequency Cepstral Coefficients) features
/// from audio signals. This is used as input for the Active Speaker Detection model.
/// 
/// This implementation mirrors the python_speech_features library behavior,
/// ensuring compatibility with the LRASD (Lip-Reading Active Speaker Detection) ONNX model.
/// 
/// PIPELINE:
///   1. Pre-emphasis filter (high-pass filter to boost high frequencies)
///   2. Framing (split signal into overlapping windows)
///   3. FFT (Fast Fourier Transform to get frequency spectrum)
///   4. Power Spectrum (magnitude squared)
///   5. Mel Filterbank (map linear frequencies to mel scale)
///   6. Log compression
///   7. DCT Type II (Discrete Cosine Transform to decorrelate)
///   8. Liftering (emphasize higher cepstral coefficients)
///   9. Append energy (replace c0 with log frame energy)
/// </summary>
public class ActiveSpeakerDetectUtils
{
    // =========================================================================
    // CONSTANTS (matching python_speech_features defaults)
    // =========================================================================

    /// <summary>Pre-emphasis coefficient for high-frequency boosting.</summary>
    private const float PREEMPH_COEFF = 0.97f;

    /// <summary>Number of mel filterbank channels.</summary>
    private const int N_FILT = 26;

    /// <summary>Number of cepstral coefficients to return.</summary>
    private const int NUMCEP = 13;

    /// <summary>Window length in seconds (25ms).</summary>
    private const float WINLEN = 0.025f;

    /// <summary>Step between consecutive frames in seconds (10ms).</summary>
    private const float WINSTEP = 0.01f;

    /// <summary>Liftering parameter for cepstral coefficient emphasis.</summary>
    private const int CEPLIFTER = 22;

    /// <summary>Whether to append log energy as the first coefficient.</summary>
    private const bool APPEND_ENERGY = true;

    // =========================================================================
    // INSTANCE FIELDS (computed at construction time)
    // =========================================================================

    /// <summary>Audio sample rate in Hz (typically 16000 for speech).</summary>
    private int samplerate;

    /// <summary>Window length in samples.</summary>
    private int winlenSamples;

    /// <summary>Frame step in samples.</summary>
    private int winstepSamples;

    /// <summary>FFT size (next power of 2 >= winlenSamples).</summary>
    private int nfft;

    /// <summary>Precomputed mel filterbank matrix [n_filt, n_fft/2+1].</summary>
    private float[,] filterbank;

    /// <summary>Precomputed lifter coefficients [NUMCEP].</summary>
    private float[] lifterCoeffs;

    // =========================================================================
    // CONSTRUCTOR
    // =========================================================================

    /// <summary>
    /// Initializes the MFCC extractor with the given sample rate.
    /// Precomputes filterbank and lifter coefficients for efficiency.
    /// </summary>
    /// <param name="samplerate">Audio sample rate in Hz. Default is 16000.</param>
    public ActiveSpeakerDetectUtils(int samplerate = 16000)
    {
        this.samplerate = samplerate;
        this.winlenSamples = Mathf.RoundToInt(WINLEN * samplerate);   // 400 samples at 16kHz
        this.winstepSamples = Mathf.RoundToInt(WINSTEP * samplerate); // 160 samples at 16kHz

        // Calculate NFFT as power of 2 >= window samples (matches base.py calculate_nfft)
        this.nfft = CalculateNfft(samplerate, WINLEN);

        // Precompute mel filterbank (expensive, do once)
        filterbank = GetFilterbanks(N_FILT, nfft, samplerate, 0, samplerate / 2);

        // Precompute lifter coefficients
        lifterCoeffs = new float[NUMCEP];
        for (int i = 0; i < NUMCEP; i++)
        {
            lifterCoeffs[i] = 1 + (CEPLIFTER / 2.0f) * Mathf.Sin(Mathf.PI * i / CEPLIFTER);
        }
    }

    // =========================================================================
    // PUBLIC API
    // =========================================================================

    /// <summary>
    /// Extracts MFCC features from an audio signal.
    /// Equivalent to python_speech_features.mfcc().
    /// </summary>
    /// <param name="signal">Raw audio samples (mono, float).</param>
    /// <returns>2D array [numFrames, 13] of MFCC coefficients.</returns>
    public float[,] MFCC(float[] signal)
    {
        // Step 1: Pre-emphasis filter (boosts high frequencies, reduces noise)
        float[] emphasized = Preemphasis(signal, PREEMPH_COEFF);

        // Step 2: Framing (split into overlapping windows with zero-padding)
        float[,] frames = Framesig(emphasized, winlenSamples, winstepSamples);
        int numFrames = frames.GetLength(0);

        // Step 3: NO window applied (default behavior in python_speech_features)

        // Step 4: Magnitude spectrum via FFT
        float[,] magspec = Magspec(frames, nfft);

        // Step 5: Power spectrum (computed from magspec to avoid double FFT)
        float[,] powspec = new float[numFrames, nfft / 2 + 1];
        for (int i = 0; i < numFrames; i++)
        {
            for (int j = 0; j <= nfft / 2; j++)
            {
                powspec[i, j] = (1.0f / nfft) * magspec[i, j] * magspec[i, j];
            }
        }

        // Step 6: Apply mel filterbank (map to perceptual frequency scale)
        float[,] feat = new float[numFrames, N_FILT];
        for (int i = 0; i < numFrames; i++)
        {
            for (int j = 0; j < N_FILT; j++)
            {
                float sum = 0;
                for (int k = 0; k < nfft / 2 + 1; k++)
                {
                    sum += powspec[i, k] * filterbank[j, k];
                }
                feat[i, j] = sum;
            }
        }

        // Step 7: Log compression (convert to decibel-like scale)
        for (int i = 0; i < numFrames; i++)
        {
            for (int j = 0; j < N_FILT; j++)
            {
                feat[i, j] = Mathf.Log(feat[i, j] + 1e-10f);  // Natural log
            }
        }

        // Step 8: DCT Type II (decorrelate filterbank energies)
        float[,] mfcc = DCTTypeII(feat, NUMCEP);  // [numFrames, NUMCEP]

        // Step 9: Liftering (emphasize higher coefficients)
        for (int i = 0; i < numFrames; i++)
        {
            for (int j = 0; j < NUMCEP; j++)
            {
                mfcc[i, j] *= lifterCoeffs[j];
            }
        }

        // Step 10: Append energy (replace c0 with log of POWER SPECTRUM energy)
        if (APPEND_ENERGY)
        {
            for (int i = 0; i < numFrames; i++)
            {
                // Calculate frame energy from power spectrum (matches Python library)
                float energy = 0;
                for (int k = 0; k < nfft / 2 + 1; k++)
                {
                    energy += powspec[i, k];
                }
                mfcc[i, 0] = Mathf.Log(energy + 1e-10f);
            }
        }

        return mfcc;  // [numFrames, 13]
    }

    // =========================================================================
    // HELPER METHODS
    // =========================================================================

    /// <summary>
    /// Calculates FFT size as the next power of 2 >= window length in samples.
    /// Matches base.py calculate_nfft behavior.
    /// </summary>
    private int CalculateNfft(int samplerate, float winlen)
    {
        int windowLengthSamples = Mathf.RoundToInt(winlen * samplerate);
        int nfft = 1;
        while (nfft < windowLengthSamples)
        {
            nfft *= 2;
        }
        return nfft;
    }

    /// <summary>
    /// Applies pre-emphasis filter: y[n] = x[n] - coeff * x[n-1]
    /// This boosts high frequencies to compensate for spectral rolloff.
    /// </summary>
    private float[] Preemphasis(float[] signal, float coeff)
    {
        float[] result = new float[signal.Length];
        result[0] = signal[0];
        for (int i = 1; i < signal.Length; i++)
        {
            result[i] = signal[i] - coeff * signal[i - 1];
        }
        return result;
    }

    /// <summary>
    /// Splits signal into overlapping frames with zero-padding.
    /// Matches sigproc.framesig() from python_speech_features.
    /// </summary>
    /// <param name="sig">Input signal.</param>
    /// <param name="frameLen">Frame length in samples.</param>
    /// <param name="frameStep">Step between frames in samples.</param>
    /// <returns>2D array [numFrames, frameLen].</returns>
    private float[,] Framesig(float[] sig, int frameLen, int frameStep)
    {
        int slen = sig.Length;
        int numFrames;

        if (slen <= frameLen)
        {
            numFrames = 1;
        }
        else
        {
            numFrames = 1 + Mathf.CeilToInt((slen - frameLen) / (float)frameStep);
        }

        int padLen = (numFrames - 1) * frameStep + frameLen;

        // Pad signal with zeros (matches Python)
        float[] paddedSig = new float[padLen];
        Array.Copy(sig, paddedSig, sig.Length);

        float[,] frames = new float[numFrames, frameLen];
        for (int i = 0; i < numFrames; i++)
        {
            for (int j = 0; j < frameLen; j++)
            {
                int idx = i * frameStep + j;
                frames[i, j] = paddedSig[idx];
            }
        }
        return frames;
    }

    /// <summary>
    /// Computes magnitude spectrum for each frame using FFT.
    /// </summary>
    /// <param name="frames">2D array of framed signals.</param>
    /// <param name="NFFT">FFT size (power of 2).</param>
    /// <returns>Magnitude spectrum [numFrames, NFFT/2+1].</returns>
    private float[,] Magspec(float[,] frames, int NFFT)
    {
        int numFrames = frames.GetLength(0);
        int frameLen = frames.GetLength(1);
        float[,] result = new float[numFrames, NFFT / 2 + 1];

        for (int i = 0; i < numFrames; i++)
        {
            // Extract frame and zero-pad to NFFT
            float[] frame = new float[NFFT];
            for (int j = 0; j < frameLen; j++)
            {
                frame[j] = frames[i, j];
            }

            // FFT
            float[] real, imag;
            FFT(frame, out real, out imag);

            // Magnitude (only first half + DC is needed for real signals)
            for (int k = 0; k <= NFFT / 2; k++)
            {
                result[i, k] = Mathf.Sqrt(real[k] * real[k] + imag[k] * imag[k]);
            }
        }
        return result;
    }

    /// <summary>
    /// Computes DCT Type II with orthonormal scaling.
    /// Matches scipy.fftpack.dct type=2, norm='ortho'.
    /// </summary>
    private float[,] DCTTypeII(float[,] input, int nCepstral)
    {
        int numFrames = input.GetLength(0);
        int nFilters = input.GetLength(1);
        float[,] result = new float[numFrames, nCepstral];

        for (int i = 0; i < numFrames; i++)
        {
            for (int k = 0; k < nCepstral; k++)
            {
                float sum = 0;
                for (int n = 0; n < nFilters; n++)
                {
                    sum += input[i, n] * Mathf.Cos(Mathf.PI * k * (n + 0.5f) / nFilters);
                }

                // Orthonormal scaling (as per scipy.fftpack.dct type=2, norm='ortho')
                if (k == 0)
                    sum *= Mathf.Sqrt(1.0f / nFilters);
                else
                    sum *= Mathf.Sqrt(2.0f / nFilters);

                result[i, k] = sum;
            }
        }
        return result;
    }

    /// <summary>
    /// Creates mel-spaced filterbank matrix.
    /// Maps linear frequency bins to mel-scale triangular filters.
    /// </summary>
    private float[,] GetFilterbanks(int nfilt, int nfft, int samplerate, int lowfreq, int highfreq)
    {
        float lowmel = Hz2Mel(lowfreq);
        float highmel = Hz2Mel(highfreq);

        // Mel points evenly spaced
        float[] melpoints = new float[nfilt + 2];
        for (int i = 0; i < nfilt + 2; i++)
        {
            melpoints[i] = lowmel + i * (highmel - lowmel) / (nfilt + 1);
        }

        // Convert to Hz then to FFT bin
        int[] bin = new int[nfilt + 2];
        for (int i = 0; i < nfilt + 2; i++)
        {
            float hz = Mel2Hz(melpoints[i]);
            bin[i] = Mathf.FloorToInt((nfft + 1) * hz / samplerate);
        }

        // Create filterbank (triangular filters)
        float[,] fbank = new float[nfilt, nfft / 2 + 1];
        for (int j = 0; j < nfilt; j++)
        {
            // Rising slope
            for (int i = bin[j]; i < bin[j + 1]; i++)
            {
                fbank[j, i] = (i - bin[j]) / (float)(bin[j + 1] - bin[j]);
            }
            // Falling slope
            for (int i = bin[j + 1]; i < bin[j + 2]; i++)
            {
                fbank[j, i] = (bin[j + 2] - i) / (float)(bin[j + 2] - bin[j + 1]);
            }
        }

        return fbank;
    }

    /// <summary>Converts Hz to Mel scale (perceptual frequency scale).</summary>
    private float Hz2Mel(float hz) 
    {
        return 2595 * Mathf.Log10(1 + hz / 700.0f);
    }

    /// <summary>Converts Mel scale back to Hz.</summary>
    private float Mel2Hz(float mel)
    {
        return 700 * (Mathf.Pow(10, mel / 2595.0f) - 1);
    }

    /// <summary>
    /// Cooley-Tukey FFT algorithm (iterative, in-place).
    /// Computes complex FFT of real input.
    /// </summary>
    /// <param name="input">Real input signal (length must be power of 2).</param>
    /// <param name="real">Output real part.</param>
    /// <param name="imag">Output imaginary part.</param>
    private void FFT(float[] input, out float[] real, out float[] imag)
    {
        int n = input.Length;
        real = new float[n];
        imag = new float[n];

        // Copy input to real part
        for (int i = 0; i < n; i++)
        {
            real[i] = input[i];
        }

        // Bit-reverse permutation (reorders elements for iterative FFT)
        int j = 0;
        for (int i = 0; i < n; i++)
        {
            if (i < j)
            {
                float temp = real[i];
                real[i] = real[j];
                real[j] = temp;
            }
            int m = n >> 1;
            while (m >= 1 && j >= m)
            {
                j -= m;
                m >>= 1;
            }
            j += m;
        }

        // Cooley-Tukey iterative butterfly operations
        for (int len = 2; len <= n; len *= 2)
        {
            float wlenReal = Mathf.Cos(2 * Mathf.PI / len);
            float wlenImag = -Mathf.Sin(2 * Mathf.PI / len);

            for (int i = 0; i < n; i += len)
            {
                float wReal = 1;
                float wImag = 0;

                for (int k = 0; k < len / 2; k++)
                {
                    int evenIdx = i + k;
                    int oddIdx = i + k + len / 2;

                    float evenReal = real[evenIdx];
                    float evenImag = imag[evenIdx];
                    float oddReal = real[oddIdx] * wReal - imag[oddIdx] * wImag;
                    float oddImag = real[oddIdx] * wImag + imag[oddIdx] * wReal;

                    real[evenIdx] = evenReal + oddReal;
                    imag[evenIdx] = evenImag + oddImag;
                    real[oddIdx] = evenReal - oddReal;
                    imag[oddIdx] = evenImag - oddImag;

                    // Update twiddle factor (rotate by wlen)
                    float nextWReal = wReal * wlenReal - wImag * wlenImag;
                    wImag = wReal * wlenImag + wImag * wlenReal;
                    wReal = nextWReal;
                }
            }
        }
    }
}
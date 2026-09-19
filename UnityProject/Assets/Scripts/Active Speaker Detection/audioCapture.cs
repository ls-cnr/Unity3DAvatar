using System.IO.Pipes;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Xml;
using UnityEngine;
using System;

/// <summary>
/// Captures audio from the default microphone and maintains a 1-second rolling buffer.
/// 
/// PIPELINE:
///   1. Start microphone recording at device native sample rate
///   2. Each frame, read new samples since last read
///   3. Convert stereo to mono if needed
///   4. Resample to target rate (16kHz) using linear interpolation
///   5. Add to circular buffer
/// 
/// USAGE:
///   - Check IsBufferFull() before calling getBuffer()
///   - getBuffer() returns ordered 1-second audio at 16kHz
/// </summary>
public class AudioCapture : MonoBehaviour
{
    // =========================================================================
    // INSPECTOR SETTINGS
    // =========================================================================

    [Header("Settings")]

    [Tooltip("Select the microphone device by name to ensure the correct mic is used.")]
    [MicrophoneDeviceSelector] // <-- Custom dropdown attribute
    public string selectedMicrophoneName = "";

    [Tooltip("Microphone gain multiplier. Adjust based on environment noise.")]
    public float micGain = 10f;

    [Tooltip("Target sample rate for output audio. 16kHz is required by the ASD model.")]
    public int targetSampleRate = 16000;

    [Tooltip("Microphone buffer length in seconds. Must be large enough to avoid overwrite before reading.")]
    public int audioClipLength = 10;

    // =========================================================================
    // MICROPHONE STATE
    // =========================================================================

    /// <summary>Unity AudioClip for microphone recording.</summary>
    private AudioClip microphoneClip;

    /// <summary>Name of the selected microphone device.</summary>
    private string device;

    /// <summary>Native sample rate of the microphone device.</summary>
    private int originalSampleRate;

    // =========================================================================
    // CIRCULAR BUFFER
    // =========================================================================

    /// <summary>Main circular buffer storing targetSampleRate samples (1 second).</summary>
    private float[] audioBuffer;

    /// <summary>Current write position in the circular buffer.</summary>
    private int currentBufferPos = 0;

    /// <summary>Start position of valid data (for ordered readout).</summary>
    private int bufferAudioStartPos = 0;

    // =========================================================================
    // SAMPLE PROCESSING BUFFERS
    // =========================================================================

    /// <summary>Number of new samples to add this frame.</summary>
    private int samplesToAdd;

    /// <summary>Resampled mono samples ready for the buffer.</summary>
    private float[] newSamples;

    /// <summary>Raw interleaved data from microphone.</summary>
    private float[] rawData;

    /// <summary>Mono-converted samples.</summary>
    private float[] mono;

    /// <summary>Resampled output buffer (larger than needed to handle any ratio).</summary>
    private float[] resampled;

    /// <summary>Ordered buffer for readout (reorders circular buffer).</summary>
    private float[] orderedBuffer;

    // =========================================================================
    // POSITION TRACKING
    // =========================================================================

    /// <summary>Last read position in the microphone clip.</summary>
    private int lastSamplePosition = 0;

    // =========================================================================
    // UNITY LIFECYCLE
    // =========================================================================

    /// <summary>
    /// Initializes microphone recording and buffers.
    /// </summary>
    void Start()
    {
        device = ResolveMicrophoneDevice(selectedMicrophoneName);
        UnityEngine.Debug.Log($"Using microphone: {device}");

        // Allocate buffers
        audioBuffer = new float[targetSampleRate];
        resampled = new float[targetSampleRate * audioClipLength];
        orderedBuffer = new float[targetSampleRate];

        // Get device capabilities and start recording
        Microphone.GetDeviceCaps(device, out _, out originalSampleRate);
        microphoneClip = Microphone.Start(device, true, audioClipLength, originalSampleRate);

        rawData = new float[microphoneClip.channels * originalSampleRate * audioClipLength];
        mono = new float[originalSampleRate * audioClipLength];

        UnityEngine.Debug.Log($"Start position: {Microphone.GetPosition(device)}");
        UnityEngine.Debug.Log($"Device sample rate: {originalSampleRate}Hz, Target sample rate: {targetSampleRate}Hz");
    }

    /// <summary>
    /// Each frame: read new microphone samples, process, and add to circular buffer.
    /// </summary>
    void Update()
    {
        (newSamples, samplesToAdd) = GetNewSamples();
        addSamplesToBuffer(newSamples, samplesToAdd);
    }

    /// <summary>
    /// Stops microphone recording when destroyed.
    /// </summary>
    void OnDestroy()
    {
        Microphone.End(device);
    }

    // =========================================================================
    // PUBLIC API
    // =========================================================================


    /// <summary>
    /// Resolves the inspector string to a valid microphone device name.
    /// </summary>
    private string ResolveMicrophoneDevice(string selectedName)
    {
        string[] devices = Microphone.devices;
        
        if (devices.Length == 0)
        {
            UnityEngine.Debug.LogError("[AudioCapture] No microphones found on this system!");
            return "";
        }

        if (!string.IsNullOrEmpty(selectedName))
        {
            foreach (string dev in devices)
            {
                if (dev == selectedName)
                {
                    return dev; // Exact match found
                }
            }
            UnityEngine.Debug.LogWarning($"[AudioCapture] Selected microphone '{selectedName}' not found. Falling back to default.");
        }
        else
        {
            UnityEngine.Debug.LogWarning("[AudioCapture] No microphone selected in Inspector. Falling back to default.");
        }

        return devices[0]; // Safe fallback to the first available microphone
    }

    /// <summary>
    /// Checks if the buffer contains a full second of audio.
    /// </summary>
    public bool IsBufferFull()
    {
        if (bufferAudioStartPos == 0 && currentBufferPos == targetSampleRate - 1) return true;
        if (bufferAudioStartPos > 0) return true;
        return false;
    }

    /// <summary>
    /// Gets the current 1-second audio buffer in chronological order.
    /// 
    /// The circular buffer is reordered so the oldest sample is at index 0
    /// and the newest sample is at index [targetSampleRate-1].
    /// </summary>
    public float[] getBuffer()
    {
        int start = bufferAudioStartPos;
        int end = currentBufferPos;

        // Copy from start to end of circular buffer
        int len1 = targetSampleRate - start;
        Array.Copy(audioBuffer, start, orderedBuffer, 0, len1);

        // Wrap around and copy remaining
        Array.Copy(audioBuffer, 0, orderedBuffer, len1, end);
        return orderedBuffer;
    }

    // =========================================================================
    // BUFFER MANAGEMENT
    // =========================================================================

    /// <summary>
    /// Adds processed samples to the circular buffer.
    /// Handles wrap-around and start position tracking.
    /// </summary>
    void addSamplesToBuffer(float[] samples, int samplesToAdd)
    {
        if (samplesToAdd == 0) return;

        int addPos = 0;

        // First fill: before buffer wraps around
        while (bufferAudioStartPos == 0 && addPos < samplesToAdd)
        {
            audioBuffer[currentBufferPos] = samples[addPos];
            currentBufferPos += 1;
            addPos += 1;
            if (currentBufferPos == targetSampleRate)
            {
                currentBufferPos = 0;
                bufferAudioStartPos += 1;
            }
        }

        // Subsequent fills: buffer is already circular
        while (addPos < samplesToAdd)
        {
            audioBuffer[currentBufferPos] = samples[addPos];
            currentBufferPos += 1;
            addPos += 1;
            bufferAudioStartPos += 1;
            if (currentBufferPos == targetSampleRate)
            {
                currentBufferPos = 0;
            }
            if (bufferAudioStartPos == targetSampleRate)
            {
                bufferAudioStartPos = 0;
            }
        }
    }

    // =========================================================================
    // SAMPLE EXTRACTION & PROCESSING
    // =========================================================================

    /// <summary>
    /// Reads new samples from the microphone since the last frame.
    /// 
    /// PROCESS:
    ///   1. Determine how many new samples are available
    ///   2. Read raw interleaved data from microphone clip
    ///   3. Convert to mono (average channels)
    ///   4. Resample to target sample rate
    /// </summary>
    private (float[] samples, int numSamples) GetNewSamples()
    {
        int clipSamples = microphoneClip.samples;
        int channels = microphoneClip.channels;
        int currentPos = Math.Max(Microphone.GetPosition(device), 0);
        currentPos = Math.Min(currentPos, clipSamples - 1);

        if (currentPos == lastSamplePosition) return (resampled, 0);

        // Calculate how many samples to read (handle wrap-around)
        int samplesToRead;
        if (currentPos > lastSamplePosition)
        {
            samplesToRead = currentPos - lastSamplePosition;
        }
        else
        {
            samplesToRead = (clipSamples - lastSamplePosition) + currentPos;
        }

        // Read raw interleaved data
        microphoneClip.GetData(rawData, lastSamplePosition);
        lastSamplePosition = currentPos;

        // Convert to mono
        mono = ConvertToMono(rawData, channels, samplesToRead);

        // Resample to 16kHz
        return (Resample(mono, originalSampleRate, targetSampleRate, samplesToRead), samplesToRead);
    }

    /// <summary>
    /// Converts interleaved multi-channel audio to mono by averaging channels.
    /// </summary>
    float[] ConvertToMono(float[] interleaved, int channels, int samplesToConvert)
    {
        for (int i = 0; i < samplesToConvert; i++)
        {
            float sum = 0f;
            for (int ch = 0; ch < channels; ch++)
                sum += interleaved[i * channels + ch];
            mono[i] = sum / channels;
        }
        return mono;
    }

    /// <summary>
    /// Resamples audio using linear interpolation.
    /// Also applies microphone gain.
    /// </summary>
    float[] Resample(float[] input, int inRate, int outRate, int samplesToResample)
    {
        if (inRate == outRate) return input;

        float ratio = (float)outRate / inRate;
        int outLen = Mathf.CeilToInt(samplesToResample * ratio);

        for (int i = 0; i < outLen; i++)
        {
            float srcIdx = i / ratio;
            int i0 = Mathf.FloorToInt(srcIdx);
            int i1 = Mathf.Min(i0 + 1, samplesToResample - 1);
            resampled[i] = Mathf.Lerp(input[i0], input[i1], srcIdx - i0);
            resampled[i] *= micGain;
        }
        return resampled;
    }
}
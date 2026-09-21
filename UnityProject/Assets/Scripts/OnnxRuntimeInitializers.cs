using UnityEngine;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Collections.Generic;

/// <summary>
/// Cross-Platform ONNX Runtime Initializer.
/// 
/// HOW TO USE:
/// 1. Create an empty GameObject in your first scene (e.g., "ONNX_Manager").
/// 2. Attach this script to it.
/// 3. Fill in the paths for the OS you are currently building for.
/// 4. The script will automatically preload the libraries in Awake().
/// </summary>
public class OnnxRuntimeInitializer : MonoBehaviour
{
    [Header("1. Windows Paths (Fill if building for Windows)")]
    [Tooltip("Example: C:/Program Files/NVIDIA GPU Computing Toolkit/CUDA/v12.9/bin")]
    public string winCudaBinPath = @"C:\Program Files\NVIDIA GPU Computing Toolkit\CUDA\v12.9\bin";

    [Tooltip("Example: C:/Program Files/NVIDIA/CUDNN/v9.20/bin/12.9/x64")]
    public string winCudnnBinPath = @"C:\Program Files\NVIDIA\CUDNN\v9.20\bin\12.9\x64";

    [Header("2. Linux Paths (Fill if building for Linux)")]
    [Tooltip("Example: /usr/local/cuda-12.9/lib64 or /usr/lib/x86_64-linux-gnu")]
    public string linuxCudaPath = "/usr/local/cuda/lib64";

    [Tooltip("Example: /usr/local/cuda/lib64 or path to libcudnn.so.9")]
    public string linuxCudnnPath = "/usr/local/cuda/lib64";

    [Header("3. ONNX Runtime Native Path (All OS)")]
    [Tooltip("Relative to project root or absolute path to the native runtime folder.")]
    public string onnxNativePath = @"Assets/Packages/Microsoft.ML.OnnxRuntime.Gpu.Windows.1.23.2/runtimes/win-x64/native";
    // Note: For Linux builds, change the above to your Linux runtime path, e.g., "runtimes/linux-x64/native"

    [Header("4. Options")]
    [Tooltip("If true, attempts to manually preload libraries. If false, relies on OS default search.")]
    public bool enablePreloading = true;

    [Tooltip("If true, keeps this GameObject alive across scene loads.")]
    public bool dontDestroyOnLoad = true;

    // =========================================================================
    // STATIC STATE (Accessible by other scripts, e.g., demoMain.cs)
    // =========================================================================
    public static bool IsInitialized { get; private set; } = false;
    public static bool CudaDeviceAvailable { get; private set; } = false;

    private void Awake()
    {
        if (IsInitialized) return;

        if (dontDestroyOnLoad)
        {
            DontDestroyOnLoad(gameObject);
        }

        UnityEngine.Debug.Log("[ONNX] Initializing with user-provided paths...");

#if UNITY_STANDALONE_WIN
        CudaDeviceAvailable = !string.IsNullOrEmpty(winCudaBinPath) && 
                              Directory.Exists(winCudaBinPath) && 
                              File.Exists(Path.Combine(winCudaBinPath, "cudart64_12.dll"));
#elif UNITY_STANDALONE_LINUX
        CudaDeviceAvailable = !string.IsNullOrEmpty(linuxCudaPath) && 
                              Directory.Exists(linuxCudaPath) && 
                              File.Exists(Path.Combine(linuxCudaPath, "libcudart.so.12"));
#elif UNITY_STANDALONE_OSX
        UnityEngine.Debug.LogWarning("[ONNX] macOS does not support NVIDIA CUDA. Falling back to CPU/CoreML.");
        CudaDeviceAvailable = false;
#endif

        if (enablePreloading)
        {
            SetupEnvironmentAndPreloadLibs();
        }

        IsInitialized = true;
        UnityEngine.Debug.Log($"[ONNX] Initialization complete. CUDA Available: {CudaDeviceAvailable}");
        UnityEngine.Debug.Log($"[ONNX] Current Platform: {Application.platform}");
    }

    private void SetupEnvironmentAndPreloadLibs()
    {
        var searchDirs = new List<string>();

#if UNITY_STANDALONE_WIN
        if (!string.IsNullOrEmpty(winCudaBinPath) && Directory.Exists(winCudaBinPath))
            searchDirs.Add(winCudaBinPath);
        if (!string.IsNullOrEmpty(winCudnnBinPath) && Directory.Exists(winCudnnBinPath))
            searchDirs.Add(winCudnnBinPath);
        searchDirs.Add(Environment.GetFolderPath(Environment.SpecialFolder.System));

#elif UNITY_STANDALONE_LINUX
        if (!string.IsNullOrEmpty(linuxCudaPath) && Directory.Exists(linuxCudaPath))
            searchDirs.Add(linuxCudaPath);
        if (!string.IsNullOrEmpty(linuxCudnnPath) && Directory.Exists(linuxCudnnPath))
            searchDirs.Add(linuxCudnnPath);
        searchDirs.Add("/usr/lib/x86_64-linux-gnu");
        searchDirs.Add("/usr/lib64");
#endif

        // Resolve ONNX Runtime path (convert relative to absolute if needed)
        string resolvedOnnxPath = onnxNativePath;
        if (!string.IsNullOrEmpty(resolvedOnnxPath) && !Path.IsPathRooted(resolvedOnnxPath))
        {
            string projectRoot = Application.dataPath.Replace("/Assets", "").Replace("\\Assets", "");
            resolvedOnnxPath = Path.Combine(projectRoot, resolvedOnnxPath);
        }

        if (!string.IsNullOrEmpty(resolvedOnnxPath) && Directory.Exists(resolvedOnnxPath))
        {
            searchDirs.Add(resolvedOnnxPath);
        }
        else
        {
            UnityEngine.Debug.LogWarning($"[ONNX] ONNX Runtime native path not found: {resolvedOnnxPath}");
        }

        UpdateLibrarySearchPaths(searchDirs);
        PreloadLibs(resolvedOnnxPath, searchDirs);
    }

    private static void UpdateLibrarySearchPaths(IEnumerable<string> dirs)
    {
        if (dirs == null) return;
        string combined = string.Join(Path.PathSeparator, dirs);
        string currentPath;

#if UNITY_STANDALONE_WIN
        currentPath = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.Process) ?? "";
        Environment.SetEnvironmentVariable("PATH", combined + Path.PathSeparator + currentPath, EnvironmentVariableTarget.Process);
        UnityEngine.Debug.Log("[ONNX] Updated Windows PATH for current process.");
#elif UNITY_STANDALONE_LINUX
        currentPath = Environment.GetEnvironmentVariable("LD_LIBRARY_PATH", EnvironmentVariableTarget.Process) ?? "";
        Environment.SetEnvironmentVariable("LD_LIBRARY_PATH", combined + Path.PathSeparator + currentPath, EnvironmentVariableTarget.Process);
        UnityEngine.Debug.Log("[ONNX] Updated Linux LD_LIBRARY_PATH for current process.");
#elif UNITY_STANDALONE_OSX
        currentPath = Environment.GetEnvironmentVariable("DYLD_LIBRARY_PATH", EnvironmentVariableTarget.Process) ?? "";
        Environment.SetEnvironmentVariable("DYLD_LIBRARY_PATH", combined + Path.PathSeparator + currentPath, EnvironmentVariableTarget.Process);
        UnityEngine.Debug.Log("[ONNX] Updated macOS DYLD_LIBRARY_PATH for current process.");
#endif
    }

    private static void PreloadLibs(string onnxPath, IEnumerable<string> searchDirs)
    {
        UnityEngine.Debug.Log("[ONNX] Preloading libraries for current platform...");

#if UNITY_STANDALONE_WIN
        // 1. Visual C++ Redistributables
        PreloadLib("msvcp140.dll", searchDirs, false);
        PreloadLib("vcruntime140.dll", searchDirs, false);
        PreloadLib("vcruntime140_1.dll", searchDirs, false);

        // 2. Core CUDA Libraries
        PreloadLib("cudart64_12.dll", searchDirs, false);
        PreloadLib("cublas64_12.dll", searchDirs, false);
        PreloadLib("cublasLt64_12.dll", searchDirs, false);
        PreloadLib("cufft64_11.dll", searchDirs, false);
        PreloadLib("curand64_10.dll", searchDirs, false);
        PreloadLib("cusparse64_12.dll", searchDirs, false);
        PreloadLib("cusolver64_11.dll", searchDirs, false);

        // 3. cuDNN 9.x Libraries
        PreloadLib("cudnn64_9.dll", searchDirs, false);
        PreloadLib("cudnn_ops64_9.dll", searchDirs, false);
        PreloadLib("cudnn_adv64_9.dll", searchDirs, false);

        // 4. ONNX Runtime Core
        if (!string.IsNullOrEmpty(onnxPath) && Directory.Exists(onnxPath))
        {
            PreloadLib(Path.Combine(onnxPath, "onnxruntime.dll"), searchDirs, true);
            PreloadLib(Path.Combine(onnxPath, "onnxruntime_providers_shared.dll"), searchDirs, false);
        }

#elif UNITY_STANDALONE_LINUX
        // 1. Core CUDA Libraries (.so)
        PreloadLib("libcudart.so.12", searchDirs, false);
        PreloadLib("libcublas.so.12", searchDirs, false);
        PreloadLib("libcublasLt.so.12", searchDirs, false);
        PreloadLib("libcufft.so.11", searchDirs, false);
        PreloadLib("libcurand.so.10", searchDirs, false);
        PreloadLib("libcusparse.so.12", searchDirs, false);
        PreloadLib("libcusolver.so.11", searchDirs, false);

        // 2. cuDNN 9.x Libraries (.so)
        PreloadLib("libcudnn.so.9", searchDirs, false);
        PreloadLib("libcudnn_ops.so.9", searchDirs, false);
        PreloadLib("libcudnn_adv.so.9", searchDirs, false);

        // 3. ONNX Runtime Core (.so)
        if (!string.IsNullOrEmpty(onnxPath) && Directory.Exists(onnxPath))
        {
            PreloadLib(Path.Combine(onnxPath, "libonnxruntime.so"), searchDirs, true);
            PreloadLib(Path.Combine(onnxPath, "libonnxruntime_providers_shared.so"), searchDirs, false);
        }

#elif UNITY_STANDALONE_OSX
        // macOS: No CUDA support. ONNX Runtime will use CPU or CoreML Execution Provider.
        UnityEngine.Debug.Log("[ONNX] macOS detected. Skipping CUDA/cuDNN preloading. Using CPU/CoreML.");
        if (!string.IsNullOrEmpty(onnxPath) && Directory.Exists(onnxPath))
        {
            PreloadLib(Path.Combine(onnxPath, "libonnxruntime.dylib"), searchDirs, true);
        }
#endif
    }

    private static bool PreloadLib(string libName, IEnumerable<string> searchDirs, bool critical)
    {
        string targetPath = libName;

        // If it's not an absolute path, search the provided directories
        if (!Path.IsPathRooted(libName))
        {
            foreach (var dir in searchDirs)
            {
                string candidate = Path.Combine(dir, libName);
                if (File.Exists(candidate))
                {
                    targetPath = candidate;
                    break;
                }
            }
        }

#if UNITY_STANDALONE_WIN
        IntPtr handle = LoadLibraryW(targetPath);
        if (handle == IntPtr.Zero)
        {
            int err = Marshal.GetLastWin32Error();
            string msg = $"[ONNX] Failed to load {Path.GetFileName(libName)} (Win32 Error {err}). Path: {targetPath}";
            if (critical) UnityEngine.Debug.LogError(msg); 
            else UnityEngine.Debug.LogWarning(msg);
            return false;
        }
#elif UNITY_STANDALONE_LINUX || UNITY_STANDALONE_OSX
        IntPtr handle = dlopen(targetPath, 0x5); // RTLD_LAZY | RTLD_GLOBAL
        if (handle == IntPtr.Zero)
        {
            string err = Marshal.PtrToStringAnsi(dlerror()) ?? "Unknown";
            string msg = $"[ONNX] Failed to load {Path.GetFileName(libName)} (dlerror: {err}). Path: {targetPath}";
            if (critical) UnityEngine.Debug.LogError(msg); 
            else UnityEngine.Debug.LogWarning(msg);
            return false;
        }
#endif
        UnityEngine.Debug.Log($"[ONNX] ✓ Preloaded: {Path.GetFileName(targetPath)}");
        return true;
    }

    // =========================================================================
    // NATIVE P/INVOKE
    // =========================================================================
#if UNITY_STANDALONE_WIN
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    static extern IntPtr LoadLibraryW(string lpFileName);
#elif UNITY_STANDALONE_LINUX || UNITY_STANDALONE_OSX
    [DllImport("libdl.so.2", SetLastError = true)]
    static extern IntPtr dlopen(string filename, int flags);
    
    [DllImport("libdl.so.2", SetLastError = true)]
    static extern IntPtr dlerror();
#endif
}
using UnityEngine;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

/// <summary>
/// Initializes the ONNX Runtime environment before any scene loads.
/// 
/// RESPONSIBILITIES:
///   1. Detects if a CUDA GPU is available (via nvidia-smi or system libraries)
///   2. Discovers system library paths (CUDA, cuDNN, VC++ Redistributables)
///   3. Preloads native libraries in correct dependency order
///   4. Sets up environment variables for DLL resolution
/// 
/// USAGE:
///   This class uses [RuntimeInitializeOnLoadMethod] to run automatically
///   before any scene loads. Other scripts can check:
///   if (OnnxRuntimeInitializer.CudaDeviceAvailable) { ... }
/// </summary>
public class OnnxRuntimeInitializer : MonoBehaviour
{
    // =========================================================================
    // CONFIGURATION
    // =========================================================================

    /// <summary>Path to bundled ONNX Runtime package relative to project root.</summary>
    private static readonly string OnnxRuntimePackagePath = @"Assets/Packages/Microsoft.ML.OnnxRuntime.Gpu.Windows.1.23.2";

    /// <summary>Subpath to native DLLs within the package.</summary>
    private static readonly string NativeDllPath = @"runtimes/win-x64/native";

    /// <summary>
    /// True if a CUDA-capable NVIDIA GPU was detected.
    /// Checked by demoMain.cs to decide between CUDA and CPU execution providers.
    /// </summary>
    public static bool CudaDeviceAvailable { get; private set; } = false;

    // =========================================================================
    // AUTO-INITIALIZATION
    // =========================================================================

    /// <summary>
    /// Runs automatically before any scene loads.
    /// Detects GPU and preloads all required native libraries.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void OnBeforeSceneLoad()
    {
        UnityEngine.Debug.Log("[ONNX] Initializing system environment...");
        CudaDeviceAvailable = CheckCudaDeviceAvailable();
        SetupEnvironmentAndPreloadLibs();
        UnityEngine.Debug.Log($"[ONNX] Initialization complete. CUDA Device Available: {CudaDeviceAvailable}");
    }

    // =========================================================================
    // GPU DETECTION
    // =========================================================================

    /// <summary>
    /// Checks if a CUDA-capable NVIDIA GPU is available.
    /// 
    /// STRATEGY:
    ///   1. Try running nvidia-smi (most reliable)
    ///   2. On Windows, check for nvcuda.dll in System32
    ///   3. On Linux, check for libcuda.so.1 in standard paths
    /// </summary>
    private static bool CheckCudaDeviceAvailable()
    {
#if UNITY_STANDALONE_OSX
        UnityEngine.Debug.LogWarning("[ONNX] macOS does not support NVIDIA CUDA. Falling back to CPU/CoreML.");
        return false;
#endif
        try
        {
            using var process = new Process();
            process.StartInfo.FileName = "nvidia-smi";
            process.StartInfo.Arguments = "-L";
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.CreateNoWindow = true;

            process.Start();
            string output = process.StandardOutput.ReadToEnd();
            bool exited = process.WaitForExit(3000);

            if (exited && process.ExitCode == 0 && output.Contains("GPU") && output.Contains("UUID"))
            {
                UnityEngine.Debug.Log("[ONNX] NVIDIA GPU detected via nvidia-smi");
                return true;
            }
            else
            {
                UnityEngine.Debug.LogWarning($"[ONNX] nvidia-smi exited with code {process.ExitCode} or returned empty output.");
            }
        }
        catch (Exception ex)
        {
            UnityEngine.Debug.LogWarning($"[ONNX] nvidia-smi check failed: {ex.Message}");
        }

#if UNITY_STANDALONE_WIN
        try
        {
            string systemDir = Environment.GetFolderPath(Environment.SpecialFolder.System);
            if (File.Exists(Path.Combine(systemDir, "nvcuda.dll")))
                UnityEngine.Debug.LogWarning("[ONNX] nvcuda.dll found but nvidia-smi failed. Assuming driver present but GPU unavailable.");
            else
                UnityEngine.Debug.LogWarning("[ONNX] nvcuda.dll not found in System32.");
        }
        catch (Exception ex) { UnityEngine.Debug.LogWarning($"[ONNX] Failed to check System32 for nvcuda.dll: {ex.Message}"); }
#else
        try
        {
            string[] stdPaths = { "/usr/lib/x86_64-linux-gnu", "/usr/lib64", "/usr/local/cuda/lib64" };
            foreach (var p in stdPaths)
                if (File.Exists(Path.Combine(p, "libcuda.so.1"))) { 
                    UnityEngine.Debug.Log("[ONNX] NVIDIA driver (libcuda.so.1) detected."); 
                    return true; 
                }
            UnityEngine.Debug.LogWarning("[ONNX] libcuda.so.1 not found in standard Linux paths.");
        }
        catch (Exception ex) { UnityEngine.Debug.LogWarning($"[ONNX] Failed to check for libcuda.so.1: {ex.Message}"); }
#endif
        return false;
    }

    // =========================================================================
    // ENVIRONMENT SETUP
    // =========================================================================

    /// <summary>
    /// Sets up library search paths and preloads all required native libraries.
    /// </summary>
    private static void SetupEnvironmentAndPreloadLibs()
    {
        // 1. Resolve bundled ONNX Runtime path
        string projectRoot = Path.GetDirectoryName(Application.dataPath);
        string onnxNativePath = Path.Combine(projectRoot, OnnxRuntimePackagePath, NativeDllPath);

        if (!Directory.Exists(onnxNativePath))
        {
            var fallback = Directory.GetDirectories(Application.dataPath, "native", SearchOption.AllDirectories)
                                      .FirstOrDefault(d => d.Contains("runtimes"));
            onnxNativePath = fallback ?? null;
        }

        if (string.IsNullOrEmpty(onnxNativePath) || !Directory.Exists(onnxNativePath))
            UnityEngine.Debug.LogError("[ONNX] ONNX Runtime native directory NOT FOUND. Preloading will fail.");

        // 2. Discover system CUDA/cuDNN paths (Explicitly targeting 12.9 & 9.20)
        var systemLibPaths = DiscoverSystemLibPaths();
        UnityEngine.Debug.Log($"[ONNX] Using {systemLibPaths.Count()} system directories for DLL resolution: {string.Join(", ", systemLibPaths)}");

        // 3. Update process library search paths
        UpdateLibrarySearchPaths(systemLibPaths);

        // 4. Preload libraries in exact dependency order
        PreloadAllSystemLibs(onnxNativePath, systemLibPaths);
    }

    /// <summary>
    /// Preloads all system libraries in the correct dependency order.
    /// Order matters: VC++ -> CUDA -> cuDNN -> ONNX Runtime
    /// </summary>
    private static void PreloadAllSystemLibs(string onnxPath, IEnumerable<string> searchDirs)
    {
        bool success = true;

        // 1. Visual C++ Redistributables (Windows only)
#if UNITY_STANDALONE_WIN
        foreach (var dll in GetVcRedistLibs())
            if (!PreloadLib(dll, searchDirs, false)) success = false;
#endif

        // 2. CUDA Runtime & Compute Libraries
        foreach (var lib in GetCudaLibs())
            if (!PreloadLib(lib, searchDirs, false)) success = false;

        // 3. cuDNN Libraries
        foreach (var lib in GetCudnnLibs())
            if (!PreloadLib(lib, searchDirs, false)) success = false;

        // 4. ONNX Runtime Core & Shared Providers
        if (!string.IsNullOrEmpty(onnxPath) && Directory.Exists(onnxPath))
        {
            foreach (var lib in GetOnnxRuntimeLibs())
            {
                string fullPath = Path.Combine(onnxPath, lib);
                if (!PreloadLib(fullPath, searchDirs, true)) success = false;
            }
        }
        else
        {
            UnityEngine.Debug.LogError("[ONNX] Cannot preload ONNX DLLs: Native directory missing.");
            success = false;
        }

        UnityEngine.Debug.Log($"[ONNX] Library preloading {(success ? "COMPLETED" : "COMPLETED WITH ERRORS. Check logs above.")}");
    }

    // =========================================================================
    // PATH DISCOVERY
    // =========================================================================

    /// <summary>
    /// Discovers system library directories for CUDA, cuDNN, and VC++ redistributables.
    /// </summary>
    private static IEnumerable<string> DiscoverSystemLibPaths()
    {
        var dirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

#if UNITY_STANDALONE_WIN
        string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);

        // Explicitly target CUDA 12.9
        string cudaPath = Path.Combine(programFiles, "NVIDIA GPU Computing Toolkit", "CUDA", "v12.9", "bin");
        if (Directory.Exists(cudaPath)) dirs.Add(cudaPath);
        else UnityEngine.Debug.LogWarning("[ONNX] Expected CUDA 12.9 path not found: " + cudaPath);

        // Explicitly target cuDNN 9.20 with correct NVIDIA 9.x nested structure: v9.20\bin\12.9\x64
        string cudnnBase = Path.Combine(programFiles, "NVIDIA", "CUDNN", "v9.20", "bin");
        string[] cudnnCandidates = new[] {
            Path.Combine(cudnnBase, "12.9", "x64"), // Primary expected path for CUDA 12.9
            Path.Combine(cudnnBase, "12.9"),         // Fallback if x64 isn't separate
            Path.Combine(cudnnBase, "x64"),          // Legacy fallback
            cudnnBase                                // Final fallback
        };

        bool foundCudnn = false;
        foreach (var candidate in cudnnCandidates)
        {
            if (Directory.Exists(candidate))
            {
                dirs.Add(candidate);
                if (!foundCudnn)
                {
                    UnityEngine.Debug.Log($"[ONNX] ✓ Found cuDNN 9.20 at: {candidate}");
                    foundCudnn = true;
                }
            }
        }

        if (!foundCudnn)
        {
            UnityEngine.Debug.LogWarning("[ONNX] Expected cuDNN 9.20 paths not found. Checked: " + string.Join(", ", cudnnCandidates));
        }

        // System fallbacks for VC++ & driver
        dirs.Add(Environment.GetFolderPath(Environment.SpecialFolder.System));
        dirs.Add(Environment.GetFolderPath(Environment.SpecialFolder.SystemX86));

#elif UNITY_STANDALONE_LINUX
        dirs.Add("/usr/local/cuda-12.9/lib64");
        dirs.Add("/usr/local/cuda/lib64");
        dirs.Add("/usr/lib/x86_64-linux-gnu");
#endif
        return dirs;
    }

    /// <summary>
    /// Updates the process environment variables to include discovered library paths.
    /// </summary>
    private static void UpdateLibrarySearchPaths(IEnumerable<string> dirs)
    {
        if (!dirs.Any()) return;

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

    // =========================================================================
    // LIBRARY PRELOADING
    // =========================================================================

    /// <summary>
    /// Preloads a single native library from search directories or absolute path.
    /// </summary>
    /// <param name="libName">Library name or absolute path.</param>
    /// <param name="searchDirs">Directories to search.</param>
    /// <param name="critical">If true, failure is logged as error; otherwise warning.</param>
    private static bool PreloadLib(string libName, IEnumerable<string> searchDirs, bool critical)
    {
        string targetPath = null;

        // 1. Try exact absolute path first
        if (Path.IsPathRooted(libName) && File.Exists(libName))
        {
            targetPath = libName;
        }
        else
        {
            // 2. Search discovered system directories for the exact file
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

        // 3. If not found in search dirs, fallback to bare name (OS default search)
        if (targetPath == null)
        {
            if (!critical) UnityEngine.Debug.LogWarning($"[ONNX] '{libName}' not found in discovered paths. Falling back to OS default search...");
            return LoadNative(libName, critical);
        }

        // 4. Load using resolved full path
        return LoadNative(targetPath, critical);
    }

    /// <summary>
    /// Platform-specific native library loading.
    /// </summary>
    private static bool LoadNative(string pathOrName, bool critical)
    {
#if UNITY_STANDALONE_WIN
        IntPtr handle = LoadLibraryW(pathOrName);
        if (handle == IntPtr.Zero)
        {
            int err = Marshal.GetLastWin32Error();
            string fileName = Path.GetFileName(pathOrName);
            bool fileExists = Path.IsPathRooted(pathOrName) && File.Exists(pathOrName);

            string msg;
            if (err == 126 && fileExists)
                msg = $"[ONNX] ERROR 126 loading {fileName}: File exists at '{pathOrName}', but one of its DEPENDENCIES is missing or incompatible.";
            else if (err == 126)
                msg = $"[ONNX] ERROR 126 loading {fileName}: DLL or dependency not found in any search path.";
            else
                msg = $"[ONNX] Failed to load {fileName} (Win32 Error {err}). Path: {pathOrName}";

            if (critical) UnityEngine.Debug.LogError(msg); else UnityEngine.Debug.LogWarning(msg);
            return false;
        }
#elif UNITY_STANDALONE_LINUX || UNITY_STANDALONE_OSX
        IntPtr handle = dlopen(pathOrName, 0x5); // RTLD_LAZY | RTLD_GLOBAL
        if (handle == IntPtr.Zero)
        {
            string err = Marshal.PtrToStringAnsi(dlerror()) ?? "Unknown";
            string msg = $"[ONNX] Failed to load {Path.GetFileName(pathOrName)} (dlerror: {err}). Path: {pathOrName}";
            if (critical) UnityEngine.Debug.LogError(msg); else UnityEngine.Debug.LogWarning(msg);
            return false;
        }
#else
        string msg = $"[ONNX] Native loading not implemented for this platform. Skipping: {Path.GetFileName(pathOrName)}";
        if (critical) UnityEngine.Debug.LogError(msg); else UnityEngine.Debug.LogWarning(msg);
        return false;
#endif
        UnityEngine.Debug.Log($"[ONNX] ✓ Preloaded: {Path.GetFileName(pathOrName)}");
        return true;
    }

    // =========================================================================
    // PLATFORM-SPECIFIC LIBRARY NAMES
    // =========================================================================

    private static IEnumerable<string> GetVcRedistLibs()
    {
#if UNITY_STANDALONE_WIN
        return new[] { "msvcp140.dll", "vcruntime140.dll", "vcruntime140_1.dll" };
#else
        return Enumerable.Empty<string>();
#endif
    }

    private static IEnumerable<string> GetCudaLibs()
    {
#if UNITY_STANDALONE_WIN
        return new[] { "cudart64_12.dll", "nvJitLink_120_0.dll", "nvrtc-builtins64_129.dll", "nvrtc64_120_0.dll",
                       "cublas64_12.dll", "cublasLt64_12.dll", "cufft64_11.dll", "curand64_10.dll",
                       "cusparse64_12.dll", "cusolver64_11.dll", "nppc64_12.dll", "npps64_12.dll" };
#elif UNITY_STANDALONE_LINUX
        return new[] { "libcudart.so.12", "libnvJitLink.so.12", "libnvrtc-builtins.so.12", "libnvrtc.so.12",
                       "libcublas.so.12", "libcublasLt.so.12", "libcufft.so.11", "libcurand.so.10",
                       "libcusparse.so.12", "libcusolver.so.11", "libnppc.so.12", "libnpps.so.12" };
#else
        return Enumerable.Empty<string>();
#endif
    }

    private static IEnumerable<string> GetCudnnLibs()
    {
#if UNITY_STANDALONE_WIN
        return new[] { "cudnn64_9.dll", "cudnn_ops64_9.dll", "cudnn_adv64_9.dll", "cudnn_heuristic64_9.dll",
                       "cudnn_engines_runtime_compiled64_9.dll", "cudnn_engines_precompiled64_9.dll", "cudnn_graph64_9.dll" };
#elif UNITY_STANDALONE_LINUX
        return new[] { "libcudnn.so.9", "libcudnn_ops.so.9", "libcudnn_adv.so.9", "libcudnn_heuristic.so.9",
                       "libcudnn_engines_runtime_compiled.so.9", "libcudnn_engines_precompiled.so.9", "libcudnn_graph.so.9" };
#else
        return Enumerable.Empty<string>();
#endif
    }

    private static IEnumerable<string> GetOnnxRuntimeLibs()
    {
#if UNITY_STANDALONE_WIN
        return new[] { "onnxruntime.dll", "onnxruntime_providers_shared.dll" };
#elif UNITY_STANDALONE_LINUX
        return new[] { "libonnxruntime.so", "libonnxruntime_providers_shared.so" };
#elif UNITY_STANDALONE_OSX
        return new[] { "libonnxruntime.dylib", "libonnxruntime_providers_shared.dylib" };
#else
        return new[] { "onnxruntime", "onnxruntime_providers_shared" };
#endif
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
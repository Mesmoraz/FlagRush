using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.Rendering;

namespace FlagRush.Demo.Editor
{
    /// <summary>Headless player builds. Web is the target that matters; Windows is the fast sanity check.</summary>
    public static class DemoBuilder
    {
        static readonly string[] Scenes = { "Assets/Scenes/SampleScene.unity" };

        /// <summary>Release build for hosting (GitHub Pages / itch.io): Brotli files plus the JS decompression
        /// fallback, so it works on hosts that cannot set Content-Encoding.</summary>
        [MenuItem("FlagRush/Build/Web release (WebGPU, threads, Brotli)")]
        public static void BuildWeb()
        {
            ApplyWebSettings();
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Brotli;
            PlayerSettings.WebGL.decompressionFallback = true;
            PlayerSettings.WebGL.showDiagnostics = false;
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.WebGL, ManagedStrippingLevel.Low); // Medium stalled at startup on Web
            Build(BuildTarget.WebGL, "Builds/Web", BuildOptions.None);
        }

        /// <summary>Development build for local testing: uncompressed, readable stack traces, diagnostics overlay.</summary>
        [MenuItem("FlagRush/Build/Web dev (uncompressed)")]
        public static void BuildWebDev()
        {
            ApplyWebSettings();
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
            PlayerSettings.WebGL.decompressionFallback = false;
            PlayerSettings.WebGL.showDiagnostics = true;
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.WebGL, ManagedStrippingLevel.Low);
            Build(BuildTarget.WebGL, "Builds/WebDev", BuildOptions.Development);
        }

        [MenuItem("FlagRush/Build/Web release (WebGL2 only)")]
        public static void BuildWebGl2()
        {
            ApplyWebSettings(webGpu: false);
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Brotli;
            PlayerSettings.WebGL.decompressionFallback = true;
            PlayerSettings.WebGL.showDiagnostics = false;
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.WebGL, ManagedStrippingLevel.Low);
            Build(BuildTarget.WebGL, "Builds/Web", BuildOptions.None);
        }

        static void ApplyWebSettings(bool webGpu = true)
        {
            // The splash blocks frames for seconds on Web: it times out the netcode handshake and skews the
            // first measurements, and it is what stalls WebGPU release builds at startup.
            PlayerSettings.SplashScreen.show = false;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.WebGL, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.WebGL, webGpu
                ? new[] { GraphicsDeviceType.WebGPU, GraphicsDeviceType.OpenGLES3 }
                : new[] { GraphicsDeviceType.OpenGLES3 });
            PlayerSettings.WebGL.threadsSupport = true;                 // "Enable Native C/C++ Multithreading"
            PlayerSettings.WebGL.template = "PROJECT:FlagRush";         // Assets/WebGLTemplates/FlagRush
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.WebGL, ScriptingImplementation.IL2CPP);
        }

        [MenuItem("FlagRush/Build/Windows (IL2CPP)")]
        public static void BuildWindows()
        {
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.IL2CPP);
            Build(BuildTarget.StandaloneWindows64, "Builds/Windows/FlagRush.exe", BuildOptions.Development);
        }

        static void Build(BuildTarget target, string location, BuildOptions options)
        {
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = Scenes,
                locationPathName = location,
                target = target,
                options = options,
            });
            var s = report.summary;
            Debug.Log($"[DemoBuilder] {target} -> {s.result} in {s.totalTime.TotalSeconds:F0}s, {s.totalSize / (1024 * 1024)} MB, errors={s.totalErrors} warnings={s.totalWarnings} at {Path.GetFullPath(location)}");
            if (Application.isBatchMode) EditorApplication.Exit(s.result == UnityEditor.Build.Reporting.BuildResult.Succeeded ? 0 : 1);
        }
    }
}

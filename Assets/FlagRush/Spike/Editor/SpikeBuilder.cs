using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.Rendering;

namespace FlagRush.Spike.Editor
{
    /// <summary>Headless player builds. Web is the target that matters; Windows is the fast sanity check.</summary>
    public static class SpikeBuilder
    {
        static readonly string[] Scenes = { "Assets/Scenes/SampleScene.unity" };

        [MenuItem("FlagRush/Build/Web (WebGPU, threads)")]
        public static void BuildWeb()
        {
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.WebGL, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.WebGL, new[] { GraphicsDeviceType.WebGPU, GraphicsDeviceType.OpenGLES3 });
            PlayerSettings.WebGL.threadsSupport = true;                         // "Enable Native C/C++ Multithreading"
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled; // plain files for the local COOP/COEP server
            PlayerSettings.WebGL.decompressionFallback = false;
            PlayerSettings.WebGL.showDiagnostics = true;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.WebGL, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.WebGL, ManagedStrippingLevel.Low);
            Build(BuildTarget.WebGL, "Builds/Web", BuildOptions.Development);
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
            Debug.Log($"[SpikeBuilder] {target} -> {s.result} in {s.totalTime.TotalSeconds:F0}s, {s.totalSize / (1024 * 1024)} MB, errors={s.totalErrors} warnings={s.totalWarnings} at {Path.GetFullPath(location)}");
            if (Application.isBatchMode) EditorApplication.Exit(s.result == UnityEditor.Build.Reporting.BuildResult.Succeeded ? 0 : 1);
        }
    }
}

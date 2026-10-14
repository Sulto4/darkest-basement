using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace DarkestBasement.EditorTools
{
    // Build-urile se fac din linia de comandă (tools/unity.sh build-win / build-mac).
    public static class BuildScript
    {
        [MenuItem("Darkest Basement/Build Windows")]
        public static void BuildWindows() => Build(BuildTarget.StandaloneWindows64, "Builds/win/DarkestBasement.exe");

        [MenuItem("Darkest Basement/Build macOS")]
        public static void BuildMac() => Build(BuildTarget.StandaloneOSX, "Builds/mac/DarkestBasement.app");

        static void Build(BuildTarget target, string path)
        {
            var options = new BuildPlayerOptions
            {
                scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
                locationPathName = path,
                target = target,
                options = BuildOptions.None,
            };
            var report = BuildPipeline.BuildPlayer(options);
            Debug.Log($"[BuildScript] {target}: {report.summary.result}, {report.summary.totalSize / (1024 * 1024)} MB, {report.summary.totalTime}");
            if (Application.isBatchMode && report.summary.result != BuildResult.Succeeded)
                EditorApplication.Exit(1);
        }
    }
}

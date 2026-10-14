using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace DarkestBasement.EditorTools
{
    // Build-urile se fac din linia de comandă (tools/unity.sh build-win / build-mac / build-linux).
    // Build-ul Linux e pentru QA-ul automat de pe HP Forge (Xvfb), nu pentru jucători.
    public static class BuildScript
    {
        [MenuItem("Darkest Basement/Build Windows")]
        public static void BuildWindows() => Build(BuildTarget.StandaloneWindows64, "Builds/win/DarkestBasement.exe");

        [MenuItem("Darkest Basement/Build macOS")]
        public static void BuildMac() => Build(BuildTarget.StandaloneOSX, "Builds/mac/DarkestBasement.app");

        [MenuItem("Darkest Basement/Build Linux (QA)")]
        public static void BuildLinux() => Build(BuildTarget.StandaloneLinux64, "Builds/linux/DarkestBasement.x86_64");

        static void Build(BuildTarget target, string path)
        {
            // Asset-urile generate din cod (șabloane de materiale, profilul de post-procesare, TMP) trebuie să existe înainte de build.
            ProjectSetup.EnsureGenerated();
            if (!File.Exists(ProjectSetup.ScenePath)) ProjectSetup.CreateMainScene();

            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (scenes.Length == 0) scenes = new[] { ProjectSetup.ScenePath };
            var options = new BuildPlayerOptions
            {
                scenes = scenes,
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

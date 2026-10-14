using DarkestBasement.Core;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace DarkestBasement.EditorTools
{
    // Reconstruiește tot ce ține de configurarea proiectului, ca să nu depindem de clicuri în editor.
    // Rulare: meniul "Darkest Basement/Setup Project" sau tools/unity.sh setup.
    public static class ProjectSetup
    {
        public const string ScenePath = "Assets/_Game/Scenes/Main.unity";
        const string RenderPipelinePath = "Assets/Settings/PC_RPAsset.asset";

        [MenuItem("Darkest Basement/Setup Project")]
        public static void Run()
        {
            CreateMainScene();
            ConfigureRenderPipeline();
            ConfigurePlayer();
            AssetDatabase.SaveAssets();
            Debug.Log("[ProjectSetup] gata");
        }

        // Scena are un singur obiect. Restul jocului îl construiește GameBootstrap din cod.
        static void CreateMainScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("GameBootstrap").AddComponent<GameBootstrap>();
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }

        // Pixel art-ul nu are nevoie de anti-aliasing (MSAA): ar înmuia marginile pixelilor.
        static void ConfigureRenderPipeline()
        {
            var urp = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(RenderPipelinePath);
            if (urp == null) { Debug.LogError("[ProjectSetup] lipsește " + RenderPipelinePath); return; }
            urp.msaaSampleCount = 1;
            urp.renderScale = 1f;
            urp.supportsHDR = true;
            EditorUtility.SetDirty(urp);
        }

        static void ConfigurePlayer()
        {
            PlayerSettings.companyName = "Radu Meleru";
            PlayerSettings.productName = "Darkest Basement";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Standalone, "ro.radumeleru.darkestbasement");
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.defaultScreenWidth = 1920;
            PlayerSettings.defaultScreenHeight = 1080;
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = true;
            PlayerSettings.visibleInBackground = true;
        }
    }
}

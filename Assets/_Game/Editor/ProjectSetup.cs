using System.IO;
using DarkestBasement.Core;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace DarkestBasement.EditorTools
{
    // Reconstruiește tot ce ține de configurarea proiectului, ca să nu depindem de clicuri în editor.
    // Rulare: meniul "Darkest Basement/Setup Project" sau tools/unity.sh setup (tools/forge.sh setup).
    public static class ProjectSetup
    {
        public const string ScenePath = "Assets/_Game/Scenes/Main.unity";
        const string RenderPipelinePath = "Assets/Settings/PC_RPAsset.asset";
        const string MaterialsFolder = "Assets/_Game/Resources/Materials";

        // Shaderele pe care le cere doar codul (prin Shader.Find), deci build-ul nu le-ar găsi singur.
        static readonly string[] CodeOnlyShaders = { "TextMeshPro/Mobile/Bitmap", "TextMeshPro/Bitmap", "TextMeshPro/Sprite", "TextMeshPro/Mobile/Distance Field" };

        [MenuItem("Darkest Basement/Setup Project")]
        public static void Run()
        {
            EnsureGenerated();
            CreateMainScene();
            ConfigureRenderPipeline();
            ConfigurePlayer();
            AssetDatabase.SaveAssets();
            Debug.Log("[ProjectSetup] gata");
        }

        // Asset-urile generate din cod de care are nevoie build-ul. Se poate rula oricând (și înainte de fiecare build).
        public static void EnsureGenerated()
        {
            ImportTmpEssentials();
            CreateMaterialTemplates();
            CreatePostFxProfile();
            IncludeCodeOnlyShaders();
            AssetDatabase.SaveAssets();
        }

        // Scena are un singur obiect. Restul jocului îl construiește GameBootstrap din cod.
        // Ceața e pornită și aici: Unity păstrează în build variantele de ceață doar dacă o scenă le folosește.
        public static void CreateMainScene()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 5.5f;
            RenderSettings.fogEndDistance = 27f;
            RenderSettings.fogColor = new Color(0.02f, 0.011f, 0.008f);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.085f, 0.055f, 0.042f);
            RenderSettings.skybox = null;
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

        // Șabloanele de materiale (vezi Core/Materials.cs): câte unul pe tip, în Resources, ca build-ul să păstreze variantele de shader.
        static void CreateMaterialTemplates()
        {
            EnsureFolder(MaterialsFolder);
            foreach (MaterialKind kind in System.Enum.GetValues(typeof(MaterialKind)))
            {
                string path = MaterialsFolder + "/T_" + kind + ".mat";
                var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (existing == null)
                {
                    AssetDatabase.CreateAsset(Materials.BuildTemplate(kind), path);
                    continue;
                }
                var shader = Shader.Find(Materials.ShaderName(kind));
                if (shader != null) existing.shader = shader;
                existing.shaderKeywords = new string[0];
                Materials.Configure(existing, kind);
                EditorUtility.SetDirty(existing);
            }
        }

        // O copie a profilului de post-procesare: build-ul păstrează variantele Bloom/Vignette/Grain doar dacă le găsește în asset-uri.
        static void CreatePostFxProfile()
        {
            if (AssetDatabase.LoadAssetAtPath<VolumeProfile>(PostFx.AssetPath) != null) return;
            EnsureFolder(Path.GetDirectoryName(PostFx.AssetPath).Replace('\\', '/'));
            var profile = PostFx.CreateProfile();
            AssetDatabase.CreateAsset(profile, PostFx.AssetPath);
            foreach (var component in profile.components)
            {
                component.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
                AssetDatabase.AddObjectToAsset(component, profile);
            }
            EditorUtility.SetDirty(profile);
        }

        // TextMeshPro are nevoie de "TMP Essential Resources" (setări + shadere). Le importăm din pachetul uGUI.
        static void ImportTmpEssentials()
        {
            if (AssetDatabase.IsValidFolder("Assets/TextMesh Pro")) return;
            var info = UnityEditor.PackageManager.PackageInfo.FindForAssetPath("Packages/com.unity.ugui");
            string package = info != null ? Path.Combine(info.resolvedPath, "Package Resources", "TMP Essential Resources.unitypackage") : null;
            if (package == null || !File.Exists(package))
            {
                Debug.LogWarning("[ProjectSetup] nu găsesc TMP Essential Resources.unitypackage (" + package + ")");
                return;
            }
            AssetDatabase.ImportPackage(package, false);
            AssetDatabase.Refresh();
            Debug.Log("[ProjectSetup] am importat TMP Essential Resources");
        }

        // Shaderele cerute doar din cod intră în lista "Always Included" (sunt mici; nu adăugăm aici shaderele URP mari).
        // Tot aici: ceața rămâne în build în toate modurile (o setăm din cod, per etaj).
        static void IncludeCodeOnlyShaders()
        {
            var settings = AssetDatabase.LoadAssetAtPath<GraphicsSettings>("ProjectSettings/GraphicsSettings.asset");
            if (settings == null) { Debug.LogWarning("[ProjectSetup] nu pot citi GraphicsSettings"); return; }
            var so = new SerializedObject(settings);
            var list = so.FindProperty("m_AlwaysIncludedShaders");
            foreach (string name in CodeOnlyShaders)
            {
                var shader = Shader.Find(name) ?? FindShaderAsset(name);
                if (shader == null) { Debug.LogWarning("[ProjectSetup] shader negăsit: " + name); continue; }
                bool present = false;
                for (int i = 0; i < list.arraySize; i++)
                    if (list.GetArrayElementAtIndex(i).objectReferenceValue == shader) present = true;
                if (present) continue;
                list.InsertArrayElementAtIndex(list.arraySize);
                list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = shader;
            }
            var fog = so.FindProperty("m_FogStripping");
            if (fog != null) fog.intValue = 1; // Custom
            foreach (string keep in new[] { "m_FogKeepLinear", "m_FogKeepExp", "m_FogKeepExp2" })
            {
                var p = so.FindProperty(keep);
                if (p != null) p.boolValue = true;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static Shader FindShaderAsset(string shaderName)
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Shader"))
            {
                var s = AssetDatabase.LoadAssetAtPath<Shader>(AssetDatabase.GUIDToAssetPath(guid));
                if (s != null && s.name == shaderName) return s;
            }
            return null;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}

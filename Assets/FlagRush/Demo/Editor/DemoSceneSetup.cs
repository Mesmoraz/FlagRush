using Unity.Scenes;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FlagRush.Demo.Editor
{
    /// <summary>
    /// One-shot, idempotent authoring for the spike: a SubScene with three baked markers, the instanced
    /// material, a camera that can see the swarm, and Build Settings pointing at the main scene.
    /// Run headless: unity run <proj> -- -executeMethod FlagRush.Demo.Editor.DemoSceneSetup.Create
    /// </summary>
    public static class DemoSceneSetup
    {
        const string MainScenePath = "Assets/Scenes/SampleScene.unity";
        const string SubScenePath = "Assets/FlagRush/Demo/DemoSubScene.unity";
        const string MaterialPath = "Assets/FlagRush/Demo/Resources/DemoMaterial.mat";
        const string OutlineMaterialPath = "Assets/FlagRush/Demo/Resources/DemoOutlineMaterial.mat";

        [MenuItem("FlagRush/Demo/Create scene setup")]
        public static void Create()
        {
            // Batch mode starts on an unsaved untitled scene; additive scene creation refuses that, so open the real one first.
            var main = EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Single);
            CreateMaterial();
            CreateOutlineMaterial();
            CreateSubScene();
            WireMainScene(main);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(MainScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log("[DemoSceneSetup] done");
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        static void CreateMaterial()
        {
            if (AssetDatabase.LoadAssetAtPath<Material>(MaterialPath) != null) return;
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            var material = new Material(shader) { enableInstancing = true };
            AssetDatabase.CreateAsset(material, MaterialPath);
        }

        // A transparent material asset keeps URP's _SURFACE_TYPE_TRANSPARENT variant in the build; a variant that only a
        // runtime-created material uses would be stripped and the outline would render opaque on the web.
        static void CreateOutlineMaterial()
        {
            if (AssetDatabase.LoadAssetAtPath<Material>(OutlineMaterialPath) != null) return;
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            var material = new Material(shader) { enableInstancing = true, color = InstancedRenderSystem.OutlineColor };
            InstancedRenderSystem.MakeTransparent(material);
            AssetDatabase.CreateAsset(material, OutlineMaterialPath);
        }

        static void CreateSubScene()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(SubScenePath) != null) return;
            var sub = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            for (int i = 0; i < 3; i++)
            {
                var go = new GameObject($"Marker{i + 1}");
                go.AddComponent<SubSceneMarkerAuthoring>().Value = i + 1;
                SceneManager.MoveGameObjectToScene(go, sub);
            }
            EditorSceneManager.SaveScene(sub, SubScenePath);
            EditorSceneManager.CloseScene(sub, true);
        }

        static void WireMainScene(Scene main)
        {
            var host = GameObject.Find("DemoSubScene");
            if (host == null)
            {
                host = new GameObject("DemoSubScene");
                var subScene = host.AddComponent<SubScene>();
                subScene.SceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(SubScenePath);
                subScene.AutoLoadScene = true;
            }

            var camera = Camera.main;
            if (camera != null)
            {
                camera.transform.position = new Vector3(0f, 26f, -34f);
                camera.transform.LookAt(Vector3.zero);
                camera.farClipPlane = 200f;
                // Level 3+ follow camera; it disables itself below Level 3, so this pose stays for Levels 1-2.
                if (camera.GetComponent<PlayerCameraFollow>() == null) camera.gameObject.AddComponent<PlayerCameraFollow>();
            }
            EditorSceneManager.MarkSceneDirty(main);
            EditorSceneManager.SaveScene(main);
        }
    }
}

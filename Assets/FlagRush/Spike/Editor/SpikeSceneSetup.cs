using Unity.Scenes;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FlagRush.Spike.Editor
{
    /// <summary>
    /// One-shot, idempotent authoring for the spike: a SubScene with three baked markers, the instanced
    /// material, a camera that can see the swarm, and Build Settings pointing at the main scene.
    /// Run headless: unity run <proj> -- -executeMethod FlagRush.Spike.Editor.SpikeSceneSetup.Create
    /// </summary>
    public static class SpikeSceneSetup
    {
        const string MainScenePath = "Assets/Scenes/SampleScene.unity";
        const string SubScenePath = "Assets/FlagRush/Spike/SpikeSubScene.unity";
        const string MaterialPath = "Assets/FlagRush/Spike/Resources/SpikeMaterial.mat";

        [MenuItem("FlagRush/Spike/Create scene setup")]
        public static void Create()
        {
            // Batch mode starts on an unsaved untitled scene; additive scene creation refuses that, so open the real one first.
            var main = EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Single);
            CreateMaterial();
            CreateSubScene();
            WireMainScene(main);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(MainScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log("[SpikeSceneSetup] done");
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        static void CreateMaterial()
        {
            if (AssetDatabase.LoadAssetAtPath<Material>(MaterialPath) != null) return;
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            var material = new Material(shader) { enableInstancing = true };
            AssetDatabase.CreateAsset(material, MaterialPath);
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
            var host = GameObject.Find("SpikeSubScene");
            if (host == null)
            {
                host = new GameObject("SpikeSubScene");
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
            }
            EditorSceneManager.MarkSceneDirty(main);
            EditorSceneManager.SaveScene(main);
        }
    }
}

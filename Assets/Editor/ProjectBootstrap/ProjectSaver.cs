using UnityEditor;
using UnityEngine;

namespace ProjectBootstrap
{
    // Forces an import + save so every asset gets its .meta file. Synchronous, so `unity run` is fine:
    //   unity run <proj> --editor-version <v> -- -executeMethod ProjectBootstrap.ProjectSaver.SaveAll
    public static class ProjectSaver
    {
        public static void SaveAll()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
            AssetDatabase.SaveAssets();
            Debug.Log("[ProjectSaver] Assets imported and saved.");
            EditorApplication.Exit(0);
        }
    }
}

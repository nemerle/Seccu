using System.IO;
using UnityEngine;
using UnityEditor;

namespace Editor
{
    public class CoHToolsMenu
    {
        [MenuItem("Tools/Reimport Last")]
        private static void NewMenuOption()
        {
            var path = "";
            var obj = Selection.activeObject;
            if (obj == null)
                path = "Assets";
            else
                path = AssetDatabase.GetAssetPath(obj.GetInstanceID());
            if (path.Length > 0)
            {
                if (Directory.Exists(path))
                {
                    Debug.Log("Folder");
                }
                else
                {
                    Debug.Log("File");
                    AssetDatabase.ImportAsset(path);
                }
            }
            else
            {
                Debug.Log("Not in assets folder");
            }
        }
    }
}
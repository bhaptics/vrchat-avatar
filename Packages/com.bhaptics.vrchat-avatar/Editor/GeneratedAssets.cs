using System;
using System.IO;
using UnityEditor;
using Object = UnityEngine.Object;

namespace bHaptics.VRChatAvatar
{
    /// The assets Apply generates, in Assets/bHaptics/Generated as <name>@bHaptics_yyyyMMdd-HHmmss.<ext>, a new name every time.
    public static class GeneratedAssets
    {
        public const string Folder = "Assets/bHaptics/Generated";
        private const string Suffix = "@bHaptics_";

        /// Returns a free path for an asset named after name at time; a name that is already one of our copies gets its suffix replaced.
        public static string GetPath(string name, string extension, DateTime time)
        {
            var index = name.IndexOf(Suffix, StringComparison.Ordinal);
            var baseName = index >= 0 ? name.Substring(0, index) : name;
            foreach (var c in Path.GetInvalidFileNameChars())
            {
                baseName = baseName.Replace(c, '_');
            }

            var path = $"{Folder}/{baseName}{Suffix}{time:yyyyMMdd-HHmmss}.{extension}";
            // Before the first Apply the folder doesn't exist, and a preview of the name needs no guard.
            return AssetDatabase.IsValidFolder(Folder) ? AssetDatabase.GenerateUniqueAssetPath(path) : path;
        }

        /// Copy source into the folder, named after name. Returns the copy.
        public static T Copy<T>(T source, string name, DateTime time) where T : Object
        {
            CreateFolder();
            var sourcePath = AssetDatabase.GetAssetPath(source);
            var path = GetPath(name, Path.GetExtension(sourcePath).TrimStart('.'), time);
            AssetDatabase.CopyAsset(sourcePath, path);
            return AssetDatabase.LoadAssetAtPath<T>(path);
        }

        public static void CreateFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/bHaptics"))
            {
                AssetDatabase.CreateFolder("Assets", "bHaptics");
            }

            if (!AssetDatabase.IsValidFolder(Folder))
            {
                AssetDatabase.CreateFolder("Assets/bHaptics", "Generated");
            }
        }
    }
}

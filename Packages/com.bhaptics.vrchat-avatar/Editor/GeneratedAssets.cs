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
        private const string LegacySuffix = ".bHapticsOSC"; // the legacy package named its FX copies <name>.bHapticsOSC

        /// Returns a free path for an asset named after name at time; a name that is already one of our copies, or a legacy copy, gets its suffix replaced.
        public static string GetPath(string name, string extension, DateTime time)
        {
            var index = name.IndexOf(Suffix, StringComparison.Ordinal);
            var baseName = index >= 0 ? name.Substring(0, index) : name;
            if (baseName.EndsWith(LegacySuffix, StringComparison.Ordinal))
            {
                baseName = baseName.Substring(0, baseName.Length - LegacySuffix.Length);
            }

            foreach (var c in Path.GetInvalidFileNameChars())
            {
                baseName = baseName.Replace(c, '_');
            }

            var path = $"{Folder}/{baseName}{Suffix}{time:yyyyMMdd-HHmmss}.{extension}";
            // Before the first Apply the folder doesn't exist, and a preview of the name needs no guard.
            return AssetDatabase.IsValidFolder(Folder) ? AssetDatabase.GenerateUniqueAssetPath(path) : path;
        }

        /// Copy source into the folder, named after name. Returns the copy; throws when source isn't saved as an asset or the copy fails.
        public static T Copy<T>(T source, string name, DateTime time) where T : Object
        {
            var sourcePath = AssetDatabase.GetAssetPath(source);
            if (string.IsNullOrEmpty(sourcePath))
            {
                throw new InvalidOperationException($"[bHaptics] {source.name} isn't saved as an asset, so it can't be copied.");
            }

            CreateFolder();
            var path = GetPath(name, Path.GetExtension(sourcePath).TrimStart('.'), time);
            if (!AssetDatabase.CopyAsset(sourcePath, path))
            {
                throw new InvalidOperationException($"[bHaptics] Couldn't copy {sourcePath} to {path}.");
            }

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

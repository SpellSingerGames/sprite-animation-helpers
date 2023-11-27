using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace SpellSinger.SpriteAnimationHelpers
{

    public class SpriteResizer : EditorWindow
    {
        [MenuItem("Assets/Find not resized PNGs", false)]
        private static void FindNotResized(MenuCommand menuCommand)
        {
            var textures = FindTexturesInSelection();
            textures.ForEach(tex =>
            {
                Debug.LogWarning($"Texture {tex} has incorrect size ({tex.width}x{tex.height})!", tex);
            });
        }

        [MenuItem("Assets/Resize PNGs to multiple of 4", false)]
        private static void Resize(MenuCommand menuCommand)
        {
            var textures = FindTexturesInSelection();
            textures.ForEach(Resize);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static List<Texture2D> FindTexturesInSelection()
        {
            var folders = Selection.objects.Where(o => o is DefaultAsset)
                .Select(AssetDatabase.GetAssetPath).ToArray();

            var texturesFromFolders = folders.Length == 0
                ? Enumerable.Empty<Texture2D>()
                : AssetDatabase.FindAssets("t:Texture2D", folders)
                    .Select(AssetDatabase.GUIDToAssetPath)
                    .Where(path => path.ToLower().EndsWith(".png"))
                    .Select(AssetDatabase.LoadAssetAtPath<Texture2D>);

            var selectedTextures = Selection.objects
                .OfType<Texture2D>()
                .Where(tex => AssetDatabase.GetAssetPath(tex).ToLower().EndsWith(".png"));

            var textures = texturesFromFolders.Concat(selectedTextures)
                .Distinct()
                .Where(tex => tex.width % 4 != 0 || tex.height % 4 != 0)
                .ToList();
            return textures;
        }

        private static void Resize(Texture2D tex)
        {
            var path = AssetDatabase.GetAssetPath(tex);

            if (!path.ToLower().EndsWith(".png"))
            {
                return;
            }

            if (tex.width % 4 == 0 && tex.height % 4 == 0)
            {
                return;
            }

            var readableTex = new Texture2D(tex.width, tex.height, tex.format, false);
            readableTex.LoadRawTextureData(tex.GetRawTextureData());
            readableTex.Apply();

            var newHeight = Mathf.CeilToInt(tex.height / 4f) * 4;
            var newWidth = Mathf.CeilToInt(tex.width / 4f) * 4;


            var output = new Texture2D(newWidth, newHeight, TextureFormat.RGBA32, false);

            output.SetPixels32(0, 0, newWidth, newHeight,
                Enumerable.Repeat(new Color32(1, 50, 100, 0), newHeight * newWidth).ToArray());

            output.SetPixels32((newWidth - tex.width) / 2, newHeight - tex.height - (newHeight - tex.height) / 2,
                tex.width, tex.height, readableTex.GetPixels32());
            output.Apply();
            File.WriteAllBytes(path, output.EncodeToPNG());
        }
    }
}
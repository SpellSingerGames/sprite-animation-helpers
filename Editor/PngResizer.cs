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
            textures.ForEach(tex => { Debug.LogWarning($"Texture {tex} has incorrect size ({tex.width}x{tex.height})!", tex); });
        }

        [MenuItem("Assets/Resize PNGs to multiple of 4/Default", false)]
        private static void Resize(MenuCommand menuCommand)
        {
            var textures = FindTexturesInSelection();
            textures.ForEach(texture2D => Resize(texture2D, false));

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        [MenuItem("Assets/Resize PNGs to multiple of 4/Stretch Center", false)]
        private static void ResizeWithExpand(MenuCommand menuCommand)
        {
            var textures = FindTexturesInSelection();
            textures.ForEach(texture2D => Resize(texture2D, true));

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

        private static void Resize(Texture2D tex, bool fromCenter)
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
            output.filterMode = FilterMode.Point;


            if (fromCenter)
            {
                var w = tex.width / 2;
                var h = tex.height / 2;
                var otherW = tex.width - w;
                var otherH = tex.height - h;
                output.SetPixels(0, 0, w, h, readableTex.GetPixels(0, 0, w, h));
                output.SetPixels(0, newHeight - otherH, w, otherH, readableTex.GetPixels(0, tex.height - otherH, w, otherH));
                output.SetPixels(newWidth - otherW, 0, otherW, h, readableTex.GetPixels(tex.width - otherW, 0, otherW, h));
                output.SetPixels(newWidth - otherW, newHeight - otherH, otherW, otherH,
                    readableTex.GetPixels(tex.width - otherW, tex.height - otherH, otherW, otherH));

                var extraWidth = newWidth - tex.width;
                var extraHeight = newHeight - tex.height;
                for (var i = 0; i < tex.width; i++)
                {
                    var color = (readableTex.GetPixel(i, h) + readableTex.GetPixel(i, otherH)) / 2;
                    var x = i < w ? i : i + extraWidth;
                    for (var j = 0; j < extraHeight; j++)
                        output.SetPixel(x, h + j, color);
                }

                for (var i = 0; i < tex.height; i++)
                {
                    var color = (readableTex.GetPixel(w, i) + readableTex.GetPixel(otherW, i)) / 2;
                    var y = i < h ? i : i + extraHeight;
                    for (var j = 0; j < extraWidth; j++)
                        output.SetPixel(w + j, y, color);
                }

                var middleColor = (readableTex.GetPixel(w, h) + readableTex.GetPixel(otherW, h) +
                                   readableTex.GetPixel(w, otherH) + readableTex.GetPixel(otherW, otherH)) / 4;

                for (var i = 0; i < extraWidth; i++)
                for (var j = 0; j < extraHeight; j++)
                    output.SetPixel(w + i, h + j, middleColor);
            }
            else
            {
                output.SetPixels32(0, 0, newWidth, newHeight,
                    Enumerable.Repeat(new Color32(0, 0, 0, 0), newHeight * newWidth).ToArray());
                output.SetPixels32((newWidth - tex.width) / 2, newHeight - tex.height - (newHeight - tex.height) / 2,
                    tex.width, tex.height, readableTex.GetPixels32());
            }

            output.Apply();
            File.WriteAllBytes(path + ".png", output.EncodeToPNG());
        }
    }
}
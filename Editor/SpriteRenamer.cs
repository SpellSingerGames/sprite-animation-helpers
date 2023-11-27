using System.Linq;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

namespace SpellSinger.SpriteAnimationHelpers
{
    public class SpriteRenamer : EditorWindow
    {
        [MenuItem("Assets/Fix Sprites Names", false)]
        private static void Rename(MenuCommand menuCommand)
        {
            AssetDatabase.Refresh();

            foreach (var texture2D in Selection.objects.OfType<Texture2D>())
            {
                Rename(texture2D);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        [MenuItem("Assets/Fix Sprites Names", true)]
        private static bool RenameValidation(MenuCommand menuCommand)
        {
            return Selection.objects.OfType<Texture2D>().Any();
        }

        private static void Rename(Texture2D tex)
        {
            var texPath = AssetDatabase.GetAssetPath(tex);
            var ti = AssetImporter.GetAtPath(texPath) as TextureImporter;
            if (ti == null)
            {
                Debug.LogError("Cannot get TextureImporter");
                return;
            }

            var factory = new SpriteDataProviderFactories();
            factory.Init();
            var dataProvider = factory.GetSpriteEditorDataProviderFromObject(tex);
            dataProvider.InitSpriteEditorDataProvider();

            var spriteRects = dataProvider.GetSpriteRects().ToList();
            var i = 0;
            foreach (var rect in spriteRects)
            {
                rect.name = tex.name + "_" + i++;
            }

            dataProvider.SetSpriteRects(spriteRects.ToArray());

            dataProvider.Apply();
            (dataProvider.targetObject as AssetImporter)!.SaveAndReimport();
        }
    }
}
using System.Drawing;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using Graphics = System.Drawing.Graphics;

namespace SpellSinger.SpriteAnimationHelpers
{
    public class SpriteAnimationFrameReplacer : EditorWindow
    {
        private Texture2D texture;
        private int frame = -1;
        private string folder;
        private string file;

        private void OnGUI()
        {
            texture = (Texture2D)EditorGUILayout.ObjectField("Texture", texture, typeof(Texture2D), false);
            frame = EditorGUILayout.IntField("Frame", frame);

            folder = EditorGUILayout.TextField("Folder", folder);
            file = EditorGUILayout.TextField("File", file);


            var buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fixedHeight = 30
            };

            if (texture && frame > -1 && folder != null && file != null && GUILayout.Button("Replace", buttonStyle))
            {
                AssetDatabase.Refresh();

                ReadFrameInfo(out var frameX, out var frameY, out var frameWidth, out var frameHeight);

                var sourcePath = AssetDatabase.GetAssetPath(texture);
                var original = Image.FromFile(sourcePath);
                var image = Image.FromFile(folder + "/" + file);

                var combined = new Bitmap(texture.width, texture.height);

                try
                {
                    using var g = Graphics.FromImage(combined);

                    g.ExcludeClip(new Rectangle(frameX, texture.height - frameY - frameHeight,
                        frameWidth, frameHeight));
                    g.DrawImage(original, 0, 0, texture.width, texture.height);
                    g.ResetClip();
                    g.DrawImage(image, frameX + (frameWidth - image.Width) / 2,
                        texture.height - frameY - frameHeight + (frameHeight - image.Height) / 2,
                        image.Width, image.Height);
                }
                finally
                {
                    AssetDatabase.Refresh();
                    original.Dispose();
                    image.Dispose();
                }

                combined.Save(sourcePath);
                AssetDatabase.Refresh();
            }
        }

        private void ReadFrameInfo(out int x, out int y, out int width, out int height)
        {
            var factory = new SpriteDataProviderFactories();
            factory.Init();
            var dataProvider = factory.GetSpriteEditorDataProviderFromObject(texture);
            dataProvider.InitSpriteEditorDataProvider();

            var spriteRects = dataProvider.GetSpriteRects().ToList();
            var rect = spriteRects[frame].rect;

            x = Mathf.RoundToInt(rect.x);
            y = Mathf.RoundToInt(rect.y);
            width = Mathf.RoundToInt(rect.width);
            height = Mathf.RoundToInt(rect.height);
        }

        [MenuItem("Window/SpellSinger/Sprite Animation Frame Replacer")]
        private static void ShowWindow()
        {
            var window = GetWindow<SpriteAnimationFrameReplacer>();
            window.titleContent = new GUIContent("Sprite Animation Frame Replacer");
            window.Show();
        }
    }
}
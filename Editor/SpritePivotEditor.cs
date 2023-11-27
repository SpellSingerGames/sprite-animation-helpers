using System.Linq;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

namespace SpellSinger.SpriteAnimationHelpers
{
    public class SpritePivotEditor : EditorWindow
    {
        [SerializeField] private Texture2D tex;
        [SerializeField] private SpriteAlignment alignment = SpriteAlignment.Custom;
        [SerializeField] private Vector2 pivot = new(0.5f, 0.5f);

        private void OnGUI()
        {
            tex = (Texture2D)EditorGUILayout.ObjectField("Texture", tex, typeof(Texture2D), false);

            alignment = (SpriteAlignment)EditorGUILayout.EnumPopup("Sprite Alignment", alignment);
            if (alignment == SpriteAlignment.Custom)
            {
                pivot = EditorGUILayout.Vector2Field("Pivot", pivot);
            }


            var buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fixedHeight = 30
            };

            if (GUILayout.Button("Update Pivot", buttonStyle))
            {
                AssetDatabase.Refresh();

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
                foreach (var rect in spriteRects)
                {
                    rect.alignment = alignment;
                    if (alignment == SpriteAlignment.Custom)
                    {
                        rect.pivot = pivot;
                    }
                }

                dataProvider.SetSpriteRects(spriteRects.ToArray());

                dataProvider.Apply();
                (dataProvider.targetObject as AssetImporter)!.SaveAndReimport();
            }
        }


        [MenuItem("Window/SpellSinger/Sprite Pivot Editor")]
        private static void ShowWindow()
        {
            var window = GetWindow<SpritePivotEditor>();
            window.titleContent = new GUIContent("Sprite Pivot Editor");
            window.Show();
        }
    }
}
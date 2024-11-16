using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using Graphics = System.Drawing.Graphics;

namespace SpellSinger.SpriteAnimationHelpers
{
    public class SpriteAnimationImporter : EditorWindow
    {
        [SerializeField] private bool useTexture;
        [SerializeField] private bool addToSameTexture;
        [SerializeField] private Texture2D sourceTexture;
        [SerializeField] private int skipLastFromTexture;
        [SerializeField] private string sourcePath;
        [SerializeField] private bool withPrefix;
        [SerializeField] private bool isButton;
        [SerializeField] private bool usePrefixAsName;
        [SerializeField] private string prefix;

        [SerializeField] private DefaultAsset outputFolder;
        [SerializeField] private bool overrideName;
        [SerializeField] private string outputName;

        [SerializeField] private int maxCols = 8;
        [SerializeField] private int padding = 2;
        [SerializeField] private bool square;

        [SerializeField] private int pixelsPerUnit = 100;

        [SerializeField] private SpriteAlignment alignment = SpriteAlignment.Center;
        [SerializeField] private Vector2 pivot = new(0.5f, 0.5f);

        private void OnGUI()
        {
            useTexture = EditorGUILayout.Toggle("Take Sprites from Texture", useTexture);
            if (useTexture)
            {
                addToSameTexture = EditorGUILayout.Toggle("Add to Same Texture", addToSameTexture);
                skipLastFromTexture = EditorGUILayout.IntField("Skip Last N from Texture", skipLastFromTexture);
                sourceTexture =
                    (Texture2D)EditorGUILayout.ObjectField("Source Texture", sourceTexture, typeof(Texture2D), false);
            }

            EditorGUILayout.BeginHorizontal();
            sourcePath = EditorGUILayout.TextField("Source Directory", sourcePath);
            var pickButtonWidth = GUILayout.Width(50);
            if (GUILayout.Button("Open", pickButtonWidth))
            {
                var picked = EditorUtility.OpenFolderPanel("Source Directory", sourcePath, "");
                if (!string.IsNullOrWhiteSpace(picked))
                    sourcePath = picked;
            }

            EditorGUILayout.EndHorizontal();


            EditorGUILayout.BeginHorizontal();
            withPrefix = EditorGUILayout.Toggle("With Prefix", withPrefix);
            if (withPrefix)
            {
                prefix = EditorGUILayout.TextField(prefix);
                if (!addToSameTexture)
                    usePrefixAsName = EditorGUILayout.Toggle("Use Prefix As Name", usePrefixAsName);
            }

            EditorGUILayout.EndHorizontal();

            isButton = EditorGUILayout.Toggle("Button", isButton);

            if (useTexture && addToSameTexture)
            {
                if (sourceTexture != null)
                {
                    var texturePath = AssetDatabase.GetAssetPath(sourceTexture);
                    var folderPath = texturePath.Substring(0, texturePath.LastIndexOf('/'));
                    outputFolder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(folderPath);
                }
                else
                    outputFolder = null;
            }
            else
            {
                outputFolder =
                    (DefaultAsset)EditorGUILayout.ObjectField("Output Folder", outputFolder,
                        typeof(DefaultAsset), false);
            }

            if (addToSameTexture)
            {
                outputName = sourceTexture != null ? sourceTexture.name : null;
            }
            else if (withPrefix && usePrefixAsName)
            {
                outputName = prefix;
            }
            else
            {
                EditorGUILayout.BeginHorizontal();
                overrideName = EditorGUILayout.Toggle("Override Name", overrideName);
                if (overrideName)
                {
                    outputName = EditorGUILayout.TextField(outputName);
                    if (GUILayout.Button("Copy From Source"))
                    {
                        outputName = GetSourceFolderName();
                    }
                }
                else
                    outputName = GetSourceFolderName();

                EditorGUILayout.EndHorizontal();
            }


            maxCols = EditorGUILayout.IntField("Max Columns", maxCols);
            padding = EditorGUILayout.IntField("Padding", padding);
            square = EditorGUILayout.Toggle("Square Texture", square);

            pixelsPerUnit = EditorGUILayout.IntField("Pixels Per Unit", pixelsPerUnit);
            alignment = (SpriteAlignment)EditorGUILayout.EnumPopup("Sprite Alignment", alignment);
            if (alignment == SpriteAlignment.Custom)
                pivot = EditorGUILayout.Vector2Field("Pivot", pivot);
            EditorGUILayout.HelpBox("Alignment and pivot are only applied when importing new texture",
                MessageType.Info);

            var buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fixedHeight = 30
            };

            if (outputFolder != null && outputName != "" && GUILayout.Button("Import", buttonStyle))
            {
                AssetDatabase.Refresh();
                var images = new List<(Image Image, string Name)>();

                if (sourceTexture != null)
                    images.AddRange(ReadTextureSprites());


                if (!string.IsNullOrWhiteSpace(sourcePath))
                {
                    images.AddRange(ReadFiles());
                }

                if (images.Count == 0)
                {
                    Debug.LogWarning("No images found");
                    return;
                }

                try
                {
                    var width = MultipleOf4(images.Select(tuple => tuple.Image.Width).Max());
                    var height = MultipleOf4(images.Select(tuple => tuple.Image.Height).Max());

                    var rows = Mathf.CeilToInt(images.Count / (float)maxCols);
                    var cols = Math.Min(maxCols, images.Count);


                    var texPath = AssetDatabase.GetAssetPath(outputFolder) + "/" + outputName + ".png";

                    var outputWidth = MultipleOf4((width + padding) * cols);
                    var outputHeight = MultipleOf4((height + padding) * rows);
                    if (square)
                        outputWidth = outputHeight = Math.Max(outputWidth, outputHeight);

                    Join(texPath, width, cols, height, rows, images, outputWidth, outputHeight);


                    Divide(texPath, width, height, rows, cols, images, outputHeight);
                }
                finally
                {
                    foreach (var tuple in images)
                    {
                        tuple.Image.Dispose();
                    }
                }
            }
        }

        private List<(Image Image, string Name)> ReadTextureSprites()
        {
            var importer = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(sourceTexture)) as TextureImporter;
            if (importer == null)
                throw new NullReferenceException("Importer is null");

            var readable = importer.isReadable;
            var compression = importer.textureCompression;

            try
            {
                importer.isReadable = true;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
                var factory = new SpriteDataProviderFactories();
                factory.Init();
                var dataProvider = factory.GetSpriteEditorDataProviderFromObject(sourceTexture);
                dataProvider.InitSpriteEditorDataProvider();

                var spriteRects = dataProvider.GetSpriteRects().ToList();

                return spriteRects
                    .Select(spriteRect => (ExtractSpriteAsImage(sourceTexture, spriteRect.rect), spriteRect.name))
                    .SkipLast(skipLastFromTexture)
                    .ToList();
            }
            finally
            {
                importer.isReadable = readable;
                importer.textureCompression = compression;
                importer.SaveAndReimport();
            }
        }

        private List<(Image Image, string Name)> ReadFiles()
        {
            var info = new DirectoryInfo(sourcePath);
            return info.GetFiles()
                .Where(file => file.Extension.Equals(".png") && (!withPrefix || file.Name.StartsWith(prefix)))
                .Select(file => (file, Name: isButton ? RenameForButton(file.Name) : file.Name))
                .OrderBy(tuple => tuple.Name)
                .Select(tuple => (Image: Image.FromFile(tuple.file.FullName), tuple.Name))
                .ToList();
        }

        private void Join(string texPath, int width, int cols, int height, int rows,
            List<(Image Image, string Name)> images, int outputWidth, int outputHeight)
        {
            var combined = new Bitmap(outputWidth, outputHeight);

            using (var g = Graphics.FromImage(combined))
            {
                g.CompositingQuality = CompositingQuality.HighQuality;

                for (var i = 0; i < rows; i++)
                {
                    for (var j = 0; j < maxCols; j++)
                    {
                        var index = i * maxCols + j;
                        if (index >= images.Count)
                        {
                            goto DONE;
                        }

                        var img = images[index].Image;
                        g.DrawImage(img, j * (width + padding) + (width - img.Width) / 2,
                            i * (height + padding) + (height - img.Height) / 2, img.Width, img.Height);
                    }
                }
            }

            DONE:

            combined.Save(texPath, ImageFormat.Png);
        }

        private void Divide(string texPath, int width, int height, int rows, int cols,
            List<(Image Image, string Name)> images, int outputHeight)
        {
            var ti = AssetImporter.GetAtPath(texPath) as TextureImporter;
            if (ti == null)
            {
                AssetDatabase.Refresh();
                ti = AssetImporter.GetAtPath(texPath) as TextureImporter;
                if (ti == null)
                {
                    Debug.LogError("Cannot get TextureImporter");
                    return;
                }
            }

            ti.isReadable = true;
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Multiple;
            ti.spritePixelsPerUnit = pixelsPerUnit;

            ti.SaveAndReimport();

            var factory = new SpriteDataProviderFactories();
            factory.Init();
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
            var dataProvider = factory.GetSpriteEditorDataProviderFromObject(tex);
            dataProvider.InitSpriteEditorDataProvider();

            var spriteRects = dataProvider.GetSpriteRects().ToList();

            var spriteNameFileIdDataProvider = dataProvider.GetDataProvider<ISpriteNameFileIdDataProvider>();
            var nameFileIdPairs = spriteNameFileIdDataProvider.GetNameFileIdPairs().ToList();

            SpriteAlignment alignment;
            Vector2 pivot;
            if (spriteRects.Count > 0)
            {
                alignment = spriteRects[0].alignment;
                pivot = spriteRects[0].pivot;
            }
            else
            {
                alignment = this.alignment;
                pivot = this.pivot;
            }

            spriteRects.Clear();
            nameFileIdPairs.Clear();

            var imagesCount = images.Count;
            var i = 0;
            for (var row = 0; row < rows; row++)
            {
                for (var col = 0; col < cols; col++)
                {
                    if (i >= imagesCount)
                    {
                        goto DONE;
                    }

                    var suffix = isButton ? images[i].Name : i.ToString();
                    var spriteRect = new SpriteRect
                    {
                        name = $"{tex.name}_{suffix}",
                        spriteID = GUID.Generate(),
                        rect = new Rect(col * (width + padding),
                            outputHeight - row * (height + padding) - height,
                            width, height),
                        alignment = alignment,
                        pivot = pivot
                    };

                    spriteRects.Add(spriteRect);
                    nameFileIdPairs.Add(new SpriteNameFileIdPair(spriteRect.name, spriteRect.spriteID));
                    i++;
                }
            }

            DONE:

            dataProvider.SetSpriteRects(spriteRects.ToArray());
            spriteNameFileIdDataProvider.SetNameFileIdPairs(nameFileIdPairs);

            dataProvider.Apply();
            (dataProvider.targetObject as AssetImporter)!.SaveAndReimport();

            ti.isReadable = false;
            ti.SaveAndReimport();
        }

        private static Image ExtractSpriteAsImage(Texture2D texture, Rect rect)
        {
            // Extract pixel data directly from the original texture
            var pixels = texture.GetPixels(
                (int)rect.x,
                (int)rect.y,
                (int)rect.width,
                (int)rect.height);

            // Create a bitmap and fill its pixels
            var bitmap = new Bitmap((int)rect.width, (int)rect.height, PixelFormat.Format32bppArgb);

            for (var y = 0; y < rect.height; y++)
            {
                for (var x = 0; x < rect.width; x++)
                {
                    var pixel = pixels[y * (int)rect.width + x];
                    var color = System.Drawing.Color.FromArgb(
                        (int)(pixel.a * 255),
                        (int)(pixel.r * 255),
                        (int)(pixel.g * 255),
                        (int)(pixel.b * 255)
                    );
                    bitmap.SetPixel(x, (int)rect.height - 1 - y, color); // Flip Y for correct orientation
                }
            }

            return bitmap;
        }

        private string GetSourceFolderName()
        {
            if (sourcePath == null)
                return "";

            return sourcePath[(sourcePath.Replace("\\", "/").LastIndexOf("/", StringComparison.Ordinal) + 1)..];
        }

        private static int MultipleOf4(int value)
        {
            if (value % 4 == 0)
            {
                return value;
            }

            return value + 4 - value % 4;
        }

        private static string RenameForButton(string name)
        {
            if (name.Contains("normal"))
                return "01_normal";
            if (name.Contains("highlighted") || name.Contains("hover"))
                return "02_highlighted";
            if (name.Contains("pressed") || name.Contains("clicked"))
                return "03_pressed";
            if (name.Contains("disabled"))
                return "04_disabled";
            return name;
        }

        [MenuItem("Window/SpellSinger/Sprite Animation Importer")]
        private static void ShowWindow()
        {
            var window = GetWindow<SpriteAnimationImporter>();
            window.titleContent = new GUIContent("Sprite Animation Importer");
            window.Show();
        }
    }
}
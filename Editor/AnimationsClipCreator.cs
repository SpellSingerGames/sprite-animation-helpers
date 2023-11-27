using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;
#if SPRITEDOW_ANIMATOR
using Elendow.SpritedowAnimator;
#endif

namespace SpellSinger.SpriteAnimationHelpers
{
    [Serializable]
    public class AnimEvent
    {
        public float time;
        public string function;
        public int intParameter;
        public float floatParameter;
    }

    [Serializable]
    public class Binding
    {
        public string path = "";
        public string propertyName = "m_LocalPosition.y";
    }

    [Serializable]
    public class SpriteBinding : Binding
    {
        public bool fadeAfterwards;
        public bool customTimes;
        public bool reverseOnSave;
        public List<Spr> values = new();
    }

    [Serializable]
    public class FloatBinding : Binding
    {
        public string type;
        public List<Pos> values;
    }

    [Serializable]
    public class Pos
    {
        public float time;
        public float pos;

        public Pos()
        {
        }

        public Pos(float time, float pos)
        {
            this.time = time;
            this.pos = pos;
        }
    }

    [Serializable]
    public class Spr
    {
        public float time;
        public Sprite sprite;

        public Spr()
        {
        }

        public Spr(float time, Sprite sprite)
        {
            this.time = time;
            this.sprite = sprite;
        }
    }

    public class AnimationClipsCreator : EditorWindow
    {
        private const string SpriteRendererType = "UnityEngine.SpriteRenderer";
        private const string ColorAlphaProperty = "m_Color.a";
        private const string SpritePropertyName = "m_Sprite";
        private string clipName;
        private float frameRate = 32;
        private bool loop;

        private DefaultAsset folder;
        private Object createdClip;

        [SerializeField] private AnimEvent[] events;
        [SerializeField] private List<SpriteBinding> spriteBindings = new();
        [SerializeField] private List<FloatBinding> floatBindings = new();

        private Vector2 scrollPos;
        private SerializedObject serialObj;

        protected void OnEnable()
        {
            ScriptableObject scriptableObj = this;
            serialObj = new SerializedObject(scriptableObj);
        }

        private void OnGUI()
        {
            foreach (var spriteBinding in spriteBindings)
            {
                if (spriteBinding.propertyName == "")
                {
                    spriteBinding.propertyName = SpritePropertyName;
                }
            }

            if (folder == null)
                folder = AssetDatabase.LoadAssetAtPath<DefaultAsset>("Assets/Animations");

            var clipSource =
                (AnimationClip)EditorGUILayout.ObjectField("Copy From Clip", null, typeof(AnimationClip), false);
            if (clipSource != null)
            {
                ReadFromSource(clipSource);
            }


#if SPRITEDOW_ANIMATOR
            var spriteSource =
                (SpriteAnimation)EditorGUILayout.ObjectField("Copy From Sprite", null, typeof(SpriteAnimation), false);
            if (spriteSource != null)
            {
                ReadFromSource(spriteSource);
            }
#endif

            SetSprites();

            var spritesSource =
                (AnimationClip)EditorGUILayout.ObjectField("Add Sprites From", null, typeof(AnimationClip), false);

            if (spritesSource != null)
            {
                spriteBindings = spriteBindings.Concat(ReadSprites(spritesSource)).ToList();
            }

            var floatsSource =
                (AnimationClip)EditorGUILayout.ObjectField("Add Floats From", null, typeof(AnimationClip), false);

            if (floatsSource != null)
            {
                floatBindings = floatBindings.Concat(ReadFloats(floatsSource)).ToList();
            }

            clipName = EditorGUILayout.TextField("Name", clipName);
            frameRate = EditorGUILayout.FloatField("Frame Rate", frameRate);
            loop = EditorGUILayout.Toggle("Loop Time", loop);

            folder = (DefaultAsset)EditorGUILayout.ObjectField("Folder", folder, typeof(DefaultAsset), false);

            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

            serialObj.Update();
            var eventsProperty = serialObj.FindProperty("events");
            EditorGUILayout.PropertyField(eventsProperty, true);

            EditorGUILayout.PropertyField(serialObj.FindProperty("spriteBindings"), true);
            EditorGUILayout.PropertyField(serialObj.FindProperty("floatBindings"), true);

            serialObj.ApplyModifiedProperties();

            EditorGUILayout.EndScrollView();

            GUILayout.Space(20);
            if (GUILayout.Button("Reset Sprites"))
            {
                foreach (var binding in spriteBindings)
                {
                    binding.values = new List<Spr>();
                }
            }

            GUILayout.Space(10);

            var buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fixedHeight = 30
            };

            if (GUILayout.Button("Make Clip", buttonStyle))
                MakeClip();

#if SPRITEDOW_ANIMATOR
            if (GUILayout.Button("Save as Sprite Animation", buttonStyle))
                SaveAsSprite();
#endif

            if (createdClip)
            {
                if (GUILayout.Button("Ping Created Clip"))
                {
                    EditorGUIUtility.PingObject(createdClip);
                }
            }
        }

#if SPRITEDOW_ANIMATOR
        private void SaveAsSprite()
        {
            if (spriteBindings.Count < 1)
            {
                Debug.LogWarning("No Sprite Bindings found");
                return;
            }

            var path = AssetDatabase.GetAssetPath(folder) + "/" + clipName + ".asset";
            var animation = AssetDatabase.LoadAssetAtPath<SpriteAnimation>(path);

            if (!animation)
            {
                animation = CreateInstance<SpriteAnimation>();
                AssetDatabase.CreateAsset(animation, path);
            }

            var frames = new List<SpriteAnimationFrame>();
            for (var i = 0; i < spriteBindings[0].values.Count; i++)
            {
                frames.Add(new SpriteAnimationFrame(spriteBindings[0].values[i].sprite,
                    i + 1 < spriteBindings[0].values.Count
                        ? Mathf.RoundToInt(spriteBindings[0].values[i + 1].time - spriteBindings[0].values[i].time)
                        : 1));
            }

            animation.Frames = frames;
            animation.FPS = Mathf.RoundToInt(frameRate);

            EditorUtility.SetDirty(animation);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            createdClip = animation;
        }
#endif

        private void SetSprites()
        {
            foreach (var spriteBinding in spriteBindings)
            {
                var texture2D =
                    (Texture2D)EditorGUILayout.ObjectField($"Sprites ({spriteBinding.path})", null, typeof(Texture2D),
                        false);

                if (texture2D != null)
                {
                    var sprites = AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(texture2D))
                        .OfType<Sprite>()
                        .ToList();

                    for (var i = 0; i < sprites.Count; i++)
                    {
                        if (i < spriteBinding.values.Count)
                            spriteBinding.values[i].sprite = sprites[i];
                        else
                            spriteBinding.values.Add(new Spr(i, sprites[i]));
                    }
                    if (spriteBinding.values.Count > sprites.Count)
                        spriteBinding.values.RemoveRange(sprites.Count, spriteBinding.values.Count - sprites.Count);
                }
            }

            if (GUILayout.Button("Add Sprite Binding"))
            {
                spriteBindings.Add(new SpriteBinding { propertyName = SpritePropertyName });
            }
        }

        private void ReadFromSource(AnimationClip source)
        {
            ReadNameAndFolder(source);
            frameRate = source.frameRate;

            spriteBindings = ReadSprites(source);
            floatBindings = ReadFloats(source);

            foreach (var spriteBinding in spriteBindings)
            {
                spriteBinding.fadeAfterwards = HasFadeFloatBinding(spriteBinding);
            }

            var clipSettings = AnimationUtility.GetAnimationClipSettings(source);
            loop = clipSettings.loopTime;
            events = AnimationUtility.GetAnimationEvents(source).Select(e => new AnimEvent
            {
                time = e.time,
                function = e.functionName,
                intParameter = e.intParameter,
                floatParameter = e.floatParameter
            }).ToArray();
        }

#if SPRITEDOW_ANIMATOR
        private void ReadFromSource(SpriteAnimation source)
        {
            ReadNameAndFolder(source);
            frameRate = source.FPS;

            var sprs = new List<Spr>();

            var duration = 0;
            var customTimes = false;
            foreach (var frame in source.Frames)
            {
                sprs.Add(new Spr(duration, frame.Sprite));
                duration += frame.Duration;
                if (frame.Duration != 1) customTimes = true;
            }

            spriteBindings = new List<SpriteBinding>
            {
                new()
                {
                    path = "",
                    values = sprs,
                    customTimes = customTimes,
                    propertyName = SpritePropertyName
                }
            };

            loop = false;
        }
#endif

        private void ReadNameAndFolder(Object source)
        {
            var sourcePath = AssetDatabase.GetAssetPath(source);
            clipName = source.name;
            folder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(
                sourcePath[..sourcePath.LastIndexOf("/", StringComparison.Ordinal)]);
        }

        private List<SpriteBinding> ReadSprites(AnimationClip source)
        {
            return AnimationUtility.GetObjectReferenceCurveBindings(source)
                .Select(objectBinding =>
                {
                    var values = AnimationUtility.GetObjectReferenceCurve(source, objectBinding)
                        .Select(k => new Spr(k.time * frameRate, (Sprite)k.value)).ToList();
                    return new SpriteBinding
                    {
                        path = objectBinding.path,
                        propertyName = objectBinding.propertyName,
                        values = values,
                        customTimes = values.Where((t, i) => Mathf.Abs(t.time - i) > 0.0001).Any()
                    };
                }).ToList();
        }

        private List<FloatBinding> ReadFloats(AnimationClip source)
        {
            return AnimationUtility.GetCurveBindings(source)
                .Select(floatBinding => new FloatBinding
                {
                    path = floatBinding.path,
                    propertyName = floatBinding.propertyName,
                    type = floatBinding.type.FullName,
                    values = AnimationUtility.GetEditorCurve(source, floatBinding).keys
                        .Select(k => new Pos(k.time * frameRate, k.value)).ToList()
                }).ToList();
        }

        private void MakeClip()
        {
            var animClip = new AnimationClip
            {
                name = clipName,
                frameRate = frameRate,
            };

            if (events != null)
            {
                AnimationUtility.SetAnimationEvents(animClip, events.Select(e => new AnimationEvent
                {
                    time = e.time,
                    functionName = e.function,
                    intParameter = e.intParameter,
                    floatParameter = e.floatParameter
                }).ToArray());
            }

            foreach (var spriteBinding in spriteBindings)
            {
                if (!spriteBinding.fadeAfterwards)
                    continue;

                floatBindings.RemoveAll(binding => IsFadeFloatBinding(binding, spriteBinding));
                var spritesCount = spriteBinding.values.Count;
                floatBindings.Add(new FloatBinding
                {
                    path = spriteBinding.path,
                    type = SpriteRendererType,
                    propertyName = ColorAlphaProperty,
                    values = new List<Pos>(new[] { new Pos(spritesCount - 1, 1), new Pos(spritesCount, 0) })
                });
            }

            SetSpriteCurves(animClip);
            SetFloatCurves(animClip);


            var clipSettings = AnimationUtility.GetAnimationClipSettings(animClip);
            clipSettings.loopTime = loop;
            AnimationUtility.SetAnimationClipSettings(animClip, clipSettings);

            var path = AssetDatabase.GetAssetPath(folder) + "/" + clipName + ".anim";

            var outputAsset = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);

            if (outputAsset != null)
            {
                EditorUtility.CopySerialized(animClip, outputAsset);
            }
            else
            {
                AssetDatabase.CreateAsset(animClip, path);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            createdClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        }

        private static bool IsFadeFloatBinding(FloatBinding floatBinding, SpriteBinding spriteBinding)
        {
            return floatBinding.path == spriteBinding.path
                   && floatBinding.type == SpriteRendererType
                   && floatBinding.propertyName == ColorAlphaProperty;
        }

        private bool HasFadeFloatBinding(SpriteBinding spriteBinding)
        {
            return floatBindings.Any(floatBinding =>
            {
                if (!IsFadeFloatBinding(floatBinding, spriteBinding))
                    return false;

                if (floatBinding.values.Count != 2)
                    return false;

                if (Math.Abs(floatBinding.values[0].time - (spriteBinding.values.Count - 1)) > float.Epsilon
                    || Math.Abs(floatBinding.values[0].pos - 1) > float.Epsilon)
                    return false;

                if (Math.Abs(floatBinding.values[1].time - spriteBinding.values.Count) > float.Epsilon
                    || Math.Abs(floatBinding.values[1].pos - 0) > float.Epsilon)
                    return false;

                return true;
            });
        }

        private void SetSpriteCurves(AnimationClip animClip)
        {
            var curveBindings = new EditorCurveBinding[spriteBindings.Count];
            var keyframes = new ObjectReferenceKeyframe[spriteBindings.Count][];

            for (var i = 0; i < spriteBindings.Count; i++)
            {
                var binding = spriteBindings[i];

                curveBindings[i] = new EditorCurveBinding
                {
                    type = typeof(SpriteRenderer),
                    path = binding.path,
                    propertyName = SpritePropertyName
                };

                if (binding.reverseOnSave)
                {
                    var reversed = binding.values.Select(spr => spr.sprite).Reverse().ToList();
                    for (var j = 0; j < reversed.Count; j++)
                        binding.values[j].sprite = reversed[j];
                    binding.reverseOnSave = false;
                }

                var spriteKeyFrames = new ObjectReferenceKeyframe[binding.values.Count];
                for (var j = 0; j < binding.values.Count; j++)
                {
                    if (!binding.customTimes)
                        binding.values[j].time = j;

                    spriteKeyFrames[j] = new ObjectReferenceKeyframe
                    {
                        time = binding.values[j].time / frameRate,
                        value = binding.values[j].sprite
                    };
                }

                keyframes[i] = spriteKeyFrames;
            }

            AnimationUtility.SetObjectReferenceCurves(animClip, curveBindings, keyframes);
        }

        private void SetFloatCurves(AnimationClip animClip)
        {
            var curveBindings = new EditorCurveBinding[floatBindings.Count];
            var curves = new AnimationCurve[floatBindings.Count];
            for (var i = 0; i < floatBindings.Count; i++)
            {
                var binding = floatBindings[i];

                curveBindings[i] = new EditorCurveBinding
                {
                    type = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(binding.type))
                        .First(type => type != null),
                    path = binding.path,
                    propertyName = binding.propertyName
                };

                var keyFrames = new Keyframe[binding.values.Count];
                for (var j = 0; j < binding.values.Count; j++)
                {
                    keyFrames[j] = new Keyframe(binding.values[j].time / frameRate, binding.values[j].pos);
                }

                var curve = new AnimationCurve(keyFrames);
                curves[i] = curve;
            }

            AnimationUtility.SetEditorCurves(animClip, curveBindings, curves);
        }

        [MenuItem("Tools/Animation Clips Creator")]
        private static void ShowWindow()
        {
            var window = GetWindow<AnimationClipsCreator>();
            window.titleContent = new GUIContent("Animation Clips Creator");
            window.Show();
        }
    }
}
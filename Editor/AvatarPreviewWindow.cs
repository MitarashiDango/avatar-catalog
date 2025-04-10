using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace MitarashiDango.AvatarCatalog
{
    public class AvatarPreviewWindow : EditorWindow
    {
        private AvatarRenderer _avatarRenderer;
        private GameObject _gameObject;
        private RenderTexture _renderTexture;
        private AnimationClip _animationClip;
        private AvatarRenderer.CameraSetting _cameraSetting = new AvatarRenderer.CameraSetting();

        private Dictionary<string, float> _defaultBlendShapes = new Dictionary<string, float>();
        private Dictionary<string, float> _animationClipBlendShapes = new Dictionary<string, float>();

        private float _xOffset = 0;
        private float _yOffset = 0;
        private float _zOffset = 0;
        private Color _backgroundColor = Color.white;

        [MenuItem("Tools/Avatar Catalog/Avatar Preview")]
        internal static void OpenWindow()
        {
            var window = GetWindow<AvatarPreviewWindow>("Avatar Preview");
            window.Show();
        }

        private void OnGUI()
        {
            InitializeAvatarRenderer();

            EditorGUILayout.LabelField(new GUIContent("アイコン生成元アバター"));
            var objectSelectionFieldRect = EditorGUILayout.GetControlRect(GUILayout.Height(EditorGUIUtility.singleLineHeight));
            var newGameObject = (GameObject)EditorGUI.ObjectField(objectSelectionFieldRect, _gameObject, typeof(GameObject), true);
            var newAnimationClip = (AnimationClip)EditorGUILayout.ObjectField(_animationClip, typeof(AnimationClip), true);

            var isTextureDirty = false;
            if (newGameObject != null && newGameObject != _gameObject)
            {
                storeDefaultBlendShapes(newGameObject);
            }
            else if (newGameObject == null)
            {
                _defaultBlendShapes.Clear();
            }

            if (_gameObject != newGameObject)
            {
                isTextureDirty = true;
                _gameObject = newGameObject;
            }

            if (newAnimationClip != null && newAnimationClip != _animationClip)
            {
                _animationClipBlendShapes.Clear();

                var cb = AnimationUtility.GetCurveBindings(newAnimationClip);
                _animationClipBlendShapes = cb.ToList()
                    .Where(cb => cb.propertyName.StartsWith("blendShape."))
                    .Select(cb =>
                    {
                        var curve = AnimationUtility.GetEditorCurve(newAnimationClip, cb);
                        return (blendShapeName: cb.propertyName.Substring("blendShape.".Length), blendShapeWeight: curve.Evaluate(1));
                    })
                    .ToDictionary(a => a.blendShapeName, a => a.blendShapeWeight);
            }
            else if (newAnimationClip == null)
            {
                _animationClipBlendShapes.Clear();
            }

            if (_animationClip != newAnimationClip)
            {
                isTextureDirty = true;
                _animationClip = newAnimationClip;
            }


            var imageRect = EditorGUILayout.GetControlRect(false, 192);
            imageRect.width = 192;

            var newXOffset = EditorGUILayout.Slider(_xOffset, -20, 20);
            if (_xOffset != newXOffset)
            {
                isTextureDirty = true;
                _xOffset = newXOffset;
            }

            var newYOffset = EditorGUILayout.Slider(_yOffset, -20, 20);
            if (_yOffset != newYOffset)
            {
                isTextureDirty = true;
                _yOffset = newYOffset;
            }

            var newZOffset = EditorGUILayout.Slider(_zOffset, -20, 20);
            if (_zOffset != newZOffset)
            {
                isTextureDirty = true;
                _zOffset = newZOffset;
            }

            var newBackgroundColor = EditorGUILayout.ColorField(_backgroundColor);
            if (!newBackgroundColor.Equals(_backgroundColor))
            {
                isTextureDirty = true;
                _backgroundColor = newBackgroundColor;
            }

            if (isTextureDirty)
            {
                RenderSampleTexture(_xOffset, _yOffset, _zOffset, _backgroundColor);
            }

            if (_renderTexture != null)
            {
                GUI.DrawTexture(imageRect, _renderTexture);
            }

            if (GUILayout.Button(new GUIContent("出力")))
            {
                var storagePath = Application.dataPath + "/" + "test.png";

                var texture = _avatarRenderer.Render(_gameObject, _cameraSetting, 512, 512, _defaultBlendShapes, _animationClipBlendShapes, false);

                var png = texture.EncodeToPNG();
                File.WriteAllBytes(storagePath, png);
            }
        }

        private void OnDisable()
        {
            _avatarRenderer?.Dispose();
            _avatarRenderer = null;
            Object.DestroyImmediate(_renderTexture);
        }

        private void InitializeAvatarRenderer()
        {
            if (_avatarRenderer != null)
            {
                return;
            }

            _avatarRenderer = new AvatarRenderer();
        }

        private void RenderSampleTexture(float xOffset, float yOffset, float zOffset, Color backgroundColor)
        {
            _cameraSetting.BackgroundColor = backgroundColor;
            _cameraSetting.PositionOffset = new Vector3(xOffset, yOffset, zOffset);
            _cameraSetting.Rotation = Quaternion.Euler(0, 180, 0);
            _cameraSetting.Scale = new Vector3(1, 1, 1);

            if (_renderTexture != null)
            {
                Object.DestroyImmediate(_renderTexture);
                _renderTexture = new RenderTexture(256, 256, 32, DefaultFormat.LDR);
                _renderTexture.hideFlags = HideFlags.HideAndDontSave;
            }
            else
            {
                _renderTexture = new RenderTexture(256, 256, 32, DefaultFormat.LDR);
                _renderTexture.hideFlags = HideFlags.HideAndDontSave;
            }

            _avatarRenderer.Render(_gameObject, _cameraSetting, _renderTexture, _defaultBlendShapes, _animationClipBlendShapes, false);
        }

        public void storeDefaultBlendShapes(GameObject go)
        {
            _defaultBlendShapes.Clear();

            var headGameObject = go.transform.Find("Body")?.gameObject;
            if (headGameObject == null)
            {
                return;
            }

            var skinnedMeshRenderer = headGameObject.GetComponent<SkinnedMeshRenderer>();
            if (skinnedMeshRenderer == null)
            {
                return;
            }

            var skinnedMesh = skinnedMeshRenderer.sharedMesh;

            for (var i = 0; i < skinnedMesh.blendShapeCount; i++)
            {
                var blendShapeName = skinnedMesh.GetBlendShapeName(i);
                var blendShapeWeight = skinnedMeshRenderer.GetBlendShapeWeight(i);
                _defaultBlendShapes.Add(blendShapeName, blendShapeWeight);
            }
        }
    }
}

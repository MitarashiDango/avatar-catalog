using MitarashiDango.AvatarCatalog.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace MitarashiDango.AvatarCatalog
{
    [CustomEditor(typeof(AvatarMetadataSettings))]
    public class AvatarMetadataSettingsEditor : Editor
    {
        [SerializeField]
        private VisualTreeAsset _mainUxmlAsset;

        public void OnEnable()
        {
            // イベント登録
            EditorSceneManager.sceneOpened -= OnSceneOpened; // 念のため一度解除
            EditorSceneManager.sceneOpened += OnSceneOpened;
        }

        public void OnDestroy()
        {
            // イベント解除
            EditorSceneManager.sceneOpened -= OnSceneOpened;
        }

        public override VisualElement CreateInspectorGUI()
        {
            if (_mainUxmlAsset == null)
            {
                return new Label("Main UXML Asset is not assigned.");
            }

            var root = _mainUxmlAsset.CloneTree();

            ApplyCustomFont(root);

            return root;
        }

        /// <summary>
        /// カスタムフォントを適用します
        /// </summary>
        private void ApplyCustomFont(VisualElement root)
        {
            var preferredFontFamilyName = FontCache.GetPreferredFontFamilyName();
            if (!string.IsNullOrEmpty(preferredFontFamilyName))
            {
                var fontAsset = FontCache.GetOrCreateFontAsset(preferredFontFamilyName);
                if (fontAsset != null) // FontAssetが取得できた場合のみ適用
                {
                    FontCache.ApplyFont(root, fontAsset);
                }
            }
        }

        private void OnSceneOpened(Scene scene, OpenSceneMode mode)
        {
            Repaint();
        }
    }
}

using MitarashiDango.AvatarCatalog.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.UIElements;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace MitarashiDango.AvatarCatalog
{
    [CustomEditor(typeof(AvatarMetadata))]
    public class AvatarMetadataEditor : Editor
    {
        private static readonly string mainUxmlGuid = "b06774cb627f52245822afc12ca190bf";

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

        private void OnSceneOpened(Scene scene, OpenSceneMode mode)
        {
            Repaint();
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

        public override VisualElement CreateInspectorGUI()
        {
            var mainUxmlAsset = MiscUtil.LoadVisualTreeAsset(mainUxmlGuid);
            if (mainUxmlAsset == null)
            {
                return new Label($"Cannot load UXML file");
            }

            var root = mainUxmlAsset.CloneTree();

            ApplyCustomFont(root);

            return root;
        }
    }
}
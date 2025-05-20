using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using MitarashiDango.AvatarCatalog.ExternalInterface;
using MitarashiDango.AvatarCatalog.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace MitarashiDango.AvatarCatalog
{
    [CustomEditor(typeof(AssetProductDetail))]
    public class AssetProductDetailEditor : Editor
    {
        [SerializeField]
        private VisualTreeAsset _mainUxmlAsset;

        private TextField _productUrlField;

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

            _productUrlField = root.Q<TextField>("product-url-field");

            var fetchProductDetailsButton = root.Q<Button>("fetch-product-details-button");
            fetchProductDetailsButton.RegisterCallback<ClickEvent>(OnFetchProductDetailsButtonClick);

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

        private void OnFetchProductDetailsButtonClick(ClickEvent ev)
        {
            if (string.IsNullOrEmpty(_productUrlField.value))
            {
                EditorUtility.DisplayDialog("エラー", "URLを入力してください", "OK");
                return;
            }

            if (!EditorUtility.DisplayDialog("確認", "製品情報を取得しますか？", "はい", "いいえ"))
            {
                return;
            }

            var uriCreated = Uri.TryCreate(_productUrlField.value, UriKind.Absolute, out var targetUrl);
            if (!uriCreated)
            {
                EditorUtility.DisplayDialog("エラー", "入力されたURLが正しくありません", "OK");
                return;
            }

            try
            {
                if (targetUrl.Host == "booth.pm" || targetUrl.Host.EndsWith(".booth.pm"))
                {
                    var item = FetchProductDetailsFromBooth(targetUrl);
                    ApplyFromBoothItem(item);
                }
            }
            catch (Exception e)
            {
                Debug.LogError(e);
            }
        }

        private BoothItem FetchProductDetailsFromBooth(Uri productUrl)
        {
            var boothItemId = GetBoothItemId(productUrl.AbsoluteUri);
            if (string.IsNullOrEmpty(boothItemId))
            {
                return null;
            }

            var request = WebRequest.Create($"https://booth.pm/ja/items/{boothItemId}.json");
            var response = request.GetResponse();
            var responseBody = response.GetResponseStream();
            var sr = new StreamReader(responseBody);
            var json = JsonUtility.FromJson<BoothItem>(sr.ReadToEnd());

            return json;
        }

        private void ApplyFromBoothItem(BoothItem item)
        {
            if (item == null)
            {
                return;
            }

            var productNameProperty = serializedObject.FindProperty("productName");
            productNameProperty.stringValue = item.name;

            var creatorNameProperty = serializedObject.FindProperty("creatorName");
            creatorNameProperty.stringValue = item?.shop?.name ?? "";

            var productUrlProperty = serializedObject.FindProperty("productUrl");
            productUrlProperty.stringValue = item.url;

            var releaseDateTimeProperty = serializedObject.FindProperty("releaseDateTime");
            releaseDateTimeProperty.stringValue = item.publishedAt;

            var tagsProperty = serializedObject.FindProperty("tags");
            tagsProperty.ClearArray();
            var tags = item.tags.Distinct().ToList();
            for (var i = 0; i < tags.Count; i++)
            {
                tagsProperty.InsertArrayElementAtIndex(i);
                var elem = tagsProperty.GetArrayElementAtIndex(i);
                elem.stringValue = tags[i].name;
            }

            var descriptionProperty = serializedObject.FindProperty("description");
            descriptionProperty.stringValue = item.description;

            serializedObject.ApplyModifiedProperties();
        }

        private static string GetBoothItemId(string url)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out Uri uri))
            {
                return null;
            }

            // ドメインが *.booth.pm に一致するか確認
            if (!Regex.IsMatch(uri.Host, @"(^|\.)booth\.pm$", RegexOptions.IgnoreCase))
            {
                return null;
            }

            // パスの中から /items/{数字} を探す
            var match = Regex.Match(uri.AbsolutePath, @"/items/(\d+)", RegexOptions.IgnoreCase);

            return match.Success ? match.Groups[1].Value : null;
        }
    }
}

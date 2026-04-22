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
        private static readonly string _mainUxmlGuid = "3a17e01bd8539ec4a8c1b725283e7e16";

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
            var mainUxmlAsset = MiscUtil.LoadVisualTreeAsset(_mainUxmlGuid);
            if (mainUxmlAsset == null)
            {
                return new Label($"Cannot load UXML file");
            }

            var root = mainUxmlAsset.CloneTree();
            UxmlLocalizer.Apply(root);

            FontCache.ApplyPreferredFont(root);

            _productUrlField = root.Q<TextField>("product-url-field");

            var rootFolderPath = root.Q<TextField>("root-foler-path");

            var rootFolderPathProperty = serializedObject.FindProperty("rootFolderPath");
            if (rootFolderPathProperty != null && string.IsNullOrEmpty(rootFolderPathProperty.stringValue))
            {
                rootFolderPath.style.visibility = Visibility.Hidden;
            }

            var fetchProductDetailsButton = root.Q<Button>("fetch-product-details-button");
            fetchProductDetailsButton.RegisterCallback<ClickEvent>(OnFetchProductDetailsButtonClick);

            return root;
        }

        private void OnSceneOpened(Scene scene, OpenSceneMode mode)
        {
            Repaint();
        }

        private void OnFetchProductDetailsButtonClick(ClickEvent ev)
        {
            if (string.IsNullOrEmpty(_productUrlField.value))
            {
                EditorUtility.DisplayDialog(
                    AcL10n.Tr("dialog.title.error"),
                    AcL10n.Tr("error.url_empty"),
                    AcL10n.Tr("dialog.button.ok"));
                return;
            }

            if (!EditorUtility.DisplayDialog(
                AcL10n.Tr("dialog.title.confirm"),
                AcL10n.Tr("confirm.fetch_product_info"),
                AcL10n.Tr("dialog.button.yes"),
                AcL10n.Tr("dialog.button.no")))
            {
                return;
            }

            var uriCreated = Uri.TryCreate(_productUrlField.value, UriKind.Absolute, out var targetUrl);
            if (!uriCreated)
            {
                EditorUtility.DisplayDialog(
                    AcL10n.Tr("dialog.title.error"),
                    AcL10n.Tr("error.url_invalid"),
                    AcL10n.Tr("dialog.button.ok"));
                return;
            }

            if (targetUrl.Host != "booth.pm" && !targetUrl.Host.EndsWith(".booth.pm"))
            {
                EditorUtility.DisplayDialog(
                    AcL10n.Tr("dialog.title.error"),
                    AcL10n.Tr("error.booth_url_required"),
                    AcL10n.Tr("dialog.button.ok"));
                return;
            }

            try
            {
                var item = FetchProductDetailsFromBooth(targetUrl);
                if (item == null)
                {
                    EditorUtility.DisplayDialog(
                        AcL10n.Tr("dialog.title.error"),
                        AcL10n.Tr("error.booth_item_id_not_found"),
                        AcL10n.Tr("dialog.button.ok"));
                    return;
                }

                ApplyFromBoothItem(item);
            }
            catch (WebException e)
            {
                Debug.LogError(e);

                string statusInfo;
                if (e.Response is HttpWebResponse httpResponse)
                {
                    statusInfo = AcL10n.Tr("error.booth_http_status", (int)httpResponse.StatusCode, httpResponse.StatusCode);
                }
                else
                {
                    statusInfo = AcL10n.Tr("error.booth_comm_status", e.Status);
                }

                EditorUtility.DisplayDialog(
                    AcL10n.Tr("dialog.title.error"),
                    AcL10n.Tr("error.booth_fetch_failed", statusInfo, e.Message),
                    AcL10n.Tr("dialog.button.ok"));
            }
            catch (Exception e)
            {
                Debug.LogError(e);
                EditorUtility.DisplayDialog(
                    AcL10n.Tr("dialog.title.error"),
                    AcL10n.Tr("error.booth_unexpected", e.Message),
                    AcL10n.Tr("dialog.button.ok"));
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
            using var response = request.GetResponse();
            using var responseBody = response.GetResponseStream();
            using var sr = new StreamReader(responseBody);
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

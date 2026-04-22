using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace MitarashiDango.AvatarCatalog
{
    /// <summary>
    /// 翻訳辞書を言語別 CSV ファイル (&lt;locale&gt;.csv、IETF BCP 47 形式) から読み込んで保持する
    /// </summary>
    internal static class LocalizationTable
    {
        /// <summary>
        /// 翻訳 CSV が配置されているフォルダ (package-relative)
        /// </summary>
        private const string LocalizationFolder = "Packages/com.matcha-soft.avatar-catalog/Editor/Localization";

        /// <summary>
        /// 言語別 CSV ファイルの命名パターン: &lt;locale&gt;.csv (例: ja-JP.csv, en-US.csv)
        /// </summary>
        private static readonly Regex FileNamePattern = new Regex(@"^([A-Za-z]{2,3}(?:-[A-Za-z0-9]{2,4})?)\.csv$", RegexOptions.Compiled);

        /// <summary>
        /// CSV ファイル名で使用する locale コードと Language enum のマッピング
        /// 新しい言語を追加する場合はここにエントリを追加し、&lt;locale&gt;.csv を配置する
        /// </summary>
        private static readonly Dictionary<string, Language> _localeCodeMap = new Dictionary<string, Language>
        {
            ["ja-JP"] = Language.Japanese,
            ["en-US"] = Language.English,
        };

        private static Dictionary<Language, Dictionary<string, string>> _tables =
            new Dictionary<Language, Dictionary<string, string>>();

        private static bool _loaded = false;

        /// <summary>
        /// 指定言語の翻訳を取得する。未定義キーは日本語フォールバック → キー自体を返す
        /// </summary>
        public static string Get(Language lang, string key)
        {
            EnsureLoaded();

            if (string.IsNullOrEmpty(key))
            {
                return key;
            }

            if (_tables.TryGetValue(lang, out var table) && table.TryGetValue(key, out var value))
            {
                return value;
            }

            // 指定言語に翻訳がなければ日本語をフォールバックとして返す
            if (lang != Language.Japanese
                && _tables.TryGetValue(Language.Japanese, out var jaTable)
                && jaTable.TryGetValue(key, out var jaValue))
            {
                return jaValue;
            }

            // 最終フォールバック: キー自体を返す (翻訳漏れの検出に役立つ)
            return key;
        }

        /// <summary>
        /// CSV を強制的に再読込する
        /// </summary>
        public static void Reload()
        {
            _loaded = false;
            EnsureLoaded();
        }

        private static void EnsureLoaded()
        {
            if (_loaded)
            {
                return;
            }
            _loaded = true;
            Load();
        }

        private static void Load()
        {
            _tables = new Dictionary<Language, Dictionary<string, string>>();

            string[] assetGuids;
            try
            {
                assetGuids = AssetDatabase.FindAssets("t:TextAsset", new[] { LocalizationFolder });
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AvatarCatalog] Failed to enumerate localization folder '{LocalizationFolder}': {ex}");
                return;
            }

            var loadedLanguages = new List<string>();

            foreach (var guid in assetGuids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(path))
                {
                    continue;
                }

                var fileName = Path.GetFileName(path);
                var match = FileNamePattern.Match(fileName);
                if (!match.Success)
                {
                    continue;
                }

                var code = match.Groups[1].Value;
                if (!_localeCodeMap.TryGetValue(code, out var lang))
                {
                    Debug.LogWarning($"[AvatarCatalog] CSV '{fileName}' has an unregistered locale code '{code}', skipping");
                    continue;
                }

                if (LoadLanguageFile(path, lang))
                {
                    loadedLanguages.Add(code);
                }
            }

            if (loadedLanguages.Count == 0)
            {
                Debug.LogWarning($"[AvatarCatalog] No localization CSV files were loaded from '{LocalizationFolder}'");
            }
        }

        private static bool LoadLanguageFile(string assetPath, Language lang)
        {
            TextAsset asset;
            try
            {
                asset = AssetDatabase.LoadAssetAtPath<TextAsset>(assetPath);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AvatarCatalog] Failed to load '{assetPath}': {ex}");
                return false;
            }

            if (asset == null)
            {
                Debug.LogWarning($"[AvatarCatalog] Could not load localization CSV: {assetPath}");
                return false;
            }

            List<List<string>> rows;
            try
            {
                rows = CsvParser.Parse(asset.text);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AvatarCatalog] Failed to parse '{assetPath}': {ex}");
                return false;
            }

            if (rows.Count < 1)
            {
                Debug.LogWarning($"[AvatarCatalog] Localization CSV is empty: {assetPath}");
                return false;
            }

            // ヘッダー行は "key, value" を想定 (バリデーションのみ)
            var header = rows[0];
            if (header.Count < 2 || header[0].Trim() != "key")
            {
                Debug.LogError($"[AvatarCatalog] Localization CSV header must be 'key, value': {assetPath}");
                return false;
            }

            var table = _tables.TryGetValue(lang, out var existing)
                ? existing
                : _tables[lang] = new Dictionary<string, string>();

            // データ行
            for (var r = 1; r < rows.Count; r++)
            {
                var row = rows[r];
                if (row.Count == 0)
                {
                    continue;
                }

                var key = row[0]?.Trim();
                if (string.IsNullOrEmpty(key))
                {
                    continue;
                }

                // 2 列目 (value) のみ使用
                table[key] = row.Count >= 2 ? row[1] : string.Empty;
            }

            return true;
        }
    }
}

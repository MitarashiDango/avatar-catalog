using System;
using UnityEditor;
using UnityEngine;

namespace MitarashiDango.AvatarCatalog
{
    /// <summary>
    /// 言語設定と翻訳文字列の取得を提供する静的 API
    /// </summary>
    [InitializeOnLoad]
    public static class AcL10n
    {
        private const string PrefKey = "MitarashiDango.AvatarCatalog.Language";

        /// <summary>
        /// 現在の表示言語
        /// </summary>
        public static Language Current { get; private set; }

        /// <summary>
        /// 言語が変更された際に発火する
        /// </summary>
        public static event Action OnLanguageChanged;

        static AcL10n()
        {
            Current = (Language)EditorPrefs.GetInt(PrefKey, (int)DetectDefault());
        }

        /// <summary>
        /// キーに対応する翻訳文字列を返す
        /// </summary>
        public static string Tr(string key)
        {
            return LocalizationTable.Get(Current, key);
        }

        /// <summary>
        /// キーに対応する翻訳文字列を <see cref="string.Format(string, object[])"/> で置換して返す
        /// </summary>
        public static string Tr(string key, params object[] args)
        {
            var format = Tr(key);
            if (args == null || args.Length == 0)
            {
                return format;
            }
            try
            {
                return string.Format(format, args);
            }
            catch (FormatException)
            {
                return format;
            }
        }

        /// <summary>
        /// 表示言語を変更し、購読者に通知する
        /// </summary>
        public static void SetLanguage(Language language)
        {
            if (Current == language)
            {
                return;
            }
            Current = language;
            EditorPrefs.SetInt(PrefKey, (int)language);
            OnLanguageChanged?.Invoke();
        }

        /// <summary>
        /// 翻訳テーブルを CSV から再読込し、UI を再構築するよう購読者に通知する。
        /// 開発時に翻訳ファイルを編集した後、Unity を再起動せずに反映させるための API。
        /// </summary>
        public static void ReloadTranslations()
        {
            LocalizationTable.Reload();
            OnLanguageChanged?.Invoke();
        }

        private static Language DetectDefault()
        {
            return Application.systemLanguage == SystemLanguage.Japanese
                ? Language.Japanese
                : Language.English;
        }
    }
}

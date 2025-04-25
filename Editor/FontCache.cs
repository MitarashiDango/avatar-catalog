using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.TextCore.Text;
using UnityEngine.UIElements;

namespace MitarashiDango.AvatarCatalog
{
    public static class FontCache
    {
        private static readonly string[] PreferredJapaneseFontsWindows = { "Meiryo UI", "Yu Gothic UI", "MS UI Gothic", "メイリオ", "游ゴシック" };
        private static readonly string[] PreferredJapaneseFontsMacOS = { "Hiragino Sans", "ヒラギノ角ゴシック ProN", "ヒラギノ角ゴシック Pro", "Hiragino Kaku Gothic ProN", "Hiragino Kaku Gothic Pro" };
        private static readonly string[] PreferredFallbackFonts = { "Segoe UI", "Arial", "Helvetica Neue", "Helvetica", "Verdana" };

        private static readonly Dictionary<string, FontAsset> _cachedFonts = new Dictionary<string, FontAsset>();

        /// <summary>
        /// 事前に定義されているフォントの中から、望ましいフォントの名称を取得する
        /// </summary>
        /// <returns>フォントが見つかった場合はフォントファミリー名、定義されているフォントが全て見つからなかった場合は空文字列を返却する。</returns>
        public static string GetPreferredFontFamilyName()
        {
            var osFontNames = Font.GetOSInstalledFontNames();
            if (osFontNames == null || osFontNames.Length == 0)
            {
                return "";
            }

            string[] searchTargetFontFamilyNames;

            if (Application.platform == RuntimePlatform.WindowsEditor)
            {
                searchTargetFontFamilyNames = PreferredJapaneseFontsWindows.Concat(PreferredFallbackFonts).ToArray();
            }
            else if (Application.platform == RuntimePlatform.OSXEditor)
            {
                searchTargetFontFamilyNames = PreferredJapaneseFontsMacOS.Concat(PreferredFallbackFonts).ToArray();
            }
            else
            {
                searchTargetFontFamilyNames = PreferredFallbackFonts;
            }

            var foundFontFamilyName = searchTargetFontFamilyNames.FirstOrDefault(fontFamilyName => osFontNames.Any(osFontName => osFontName.Equals(fontFamilyName, System.StringComparison.OrdinalIgnoreCase)));
            if (foundFontFamilyName != null || foundFontFamilyName != "")
            {
                return foundFontFamilyName;
            }

            return "";
        }

        /// <summary>
        /// フォントファミリー名に該当するフォントから、フォントアセットを作成する
        /// </summary>
        /// <param name="familyName">検索するフォントファミリー名</param>
        /// <returns>フォントアセット, 該当するフォントがない場合は null を返却する。</returns>
        public static FontAsset GetOrCreateFontAsset(string familyName)
        {
            if (_cachedFonts.TryGetValue(familyName, out var cachedFont))
            {
                if (cachedFont != null)
                {
                    return cachedFont;
                }
                else
                {
                    _cachedFonts.Remove(familyName);
                }
            }

            var osFontNames = Font.GetOSInstalledFontNames();
            if (osFontNames == null || osFontNames.Length == 0 || !osFontNames.ToList().Exists(osFontName => osFontName == familyName))
            {
                return null;
            }

            var fontAsset = FontAsset.CreateFontAsset(familyName, "");
            if (fontAsset == null)
            {
                return null;
            }

            fontAsset.name = $"{familyName} (Auto Generated)";
            _cachedFonts.Add(familyName, fontAsset);

            return fontAsset;
        }

        /// <summary>
        /// フォントアセットをUI Elementsへ適用する
        /// </summary>
        /// <param name="element">適用対象エレメント</param>
        /// <param name="fontAsset">適用するフォントアセット</param>
        public static void ApplyFont(VisualElement element, FontAsset fontAsset)
        {
            if (element == null || fontAsset == null)
            {
                return;
            }

            var sfd = new StyleFontDefinition(fontAsset);
            element.style.unityFontDefinition = sfd;
        }
    }
}

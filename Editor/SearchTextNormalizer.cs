using System.Text;

namespace MitarashiDango.AvatarCatalog
{
    /// <summary>
    /// 検索用テキストを比較に適した正規形に変換する
    /// </summary>
    public static class SearchTextNormalizer
    {
        /// <summary>
        /// 検索テキストを比較用の正規形に変換する。インデックス側・クエリ側の双方で同じ変換を適用すること。
        /// 実施内容:
        ///   1. Unicode 互換分解 (FormKC)
        ///      - 半角カタカナ (ｱ) → 全角カタカナ (ア)
        ///      - 全角英数 (Ａ) → 半角英数 (A)
        ///      - 全角スペース (U+3000) → 半角スペース
        ///      - 互換文字 (① など) → 基本形
        ///   2. カルチャ非依存の小文字化 (ToLowerInvariant)
        ///   3. カタカナ → ひらがな変換 (検索においてひらがな・カタカナを同一視する)
        /// </summary>
        public static string Normalize(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return text;
            }

            var normalized = text.Normalize(System.Text.NormalizationForm.FormKC).ToLowerInvariant();

            var sb = new StringBuilder(normalized.Length);
            foreach (var c in normalized)
            {
                // カタカナ (U+30A1..U+30F6) → ひらがな (U+3041..U+3096)
                // 0x60 差で一対一対応するため、オフセット減算で変換できる
                if (c >= '\u30A1' && c <= '\u30F6')
                {
                    sb.Append((char)(c - 0x60));
                }
                else
                {
                    sb.Append(c);
                }
            }
            return sb.ToString();
        }
    }
}

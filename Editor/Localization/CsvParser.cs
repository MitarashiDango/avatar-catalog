using System.Collections.Generic;
using System.Text;

namespace MitarashiDango.AvatarCatalog
{
    /// <summary>
    /// 軽量 CSV パーサー
    /// 対応機能:
    ///   - フィールドのダブルクォート括りと、その中の二重クォート ("") によるエスケープ
    ///   - クォート内部の改行許容
    ///   - バックスラッシュエスケープ: \n, \r, \t, \\ (クォート内部のみ)
    ///   - BOM 除去
    /// </summary>
    internal static class CsvParser
    {
        /// <summary>
        /// CSV テキストを行のリストへ分解する。各行はフィールド文字列のリスト。
        /// </summary>
        public static List<List<string>> Parse(string csv)
        {
            var rows = new List<List<string>>();
            if (string.IsNullOrEmpty(csv))
            {
                return rows;
            }

            // UTF-8 BOM を除去
            if (csv[0] == '\uFEFF')
            {
                csv = csv.Substring(1);
            }

            var currentRow = new List<string>();
            var currentField = new StringBuilder();
            var inQuotes = false;
            var hasContent = false;

            var i = 0;
            while (i < csv.Length)
            {
                var c = csv[i];

                if (inQuotes)
                {
                    if (c == '"')
                    {
                        // 二重クォートはエスケープとして '"' を 1 文字出力
                        if (i + 1 < csv.Length && csv[i + 1] == '"')
                        {
                            currentField.Append('"');
                            i += 2;
                            continue;
                        }
                        // クォート終端
                        inQuotes = false;
                        i++;
                        continue;
                    }

                    if (c == '\\' && i + 1 < csv.Length)
                    {
                        var next = csv[i + 1];
                        switch (next)
                        {
                            case 'n':
                                currentField.Append('\n');
                                i += 2;
                                continue;
                            case 'r':
                                currentField.Append('\r');
                                i += 2;
                                continue;
                            case 't':
                                currentField.Append('\t');
                                i += 2;
                                continue;
                            case '\\':
                                currentField.Append('\\');
                                i += 2;
                                continue;
                            default:
                                // 未知のエスケープはそのまま保持
                                currentField.Append(c);
                                i++;
                                continue;
                        }
                    }

                    currentField.Append(c);
                    i++;
                    continue;
                }

                // クォート外
                if (c == '"')
                {
                    inQuotes = true;
                    hasContent = true;
                    i++;
                    continue;
                }

                if (c == ',')
                {
                    currentRow.Add(currentField.ToString());
                    currentField.Clear();
                    hasContent = true;
                    i++;
                    continue;
                }

                if (c == '\n')
                {
                    currentRow.Add(currentField.ToString());
                    currentField.Clear();
                    if (hasContent || currentRow.Count > 1)
                    {
                        rows.Add(currentRow);
                    }
                    currentRow = new List<string>();
                    hasContent = false;
                    i++;
                    continue;
                }

                if (c == '\r')
                {
                    // CRLF の CR は無視 (後続 LF で行末処理)
                    // 単独 CR の場合も LF 相当に扱う
                    if (i + 1 < csv.Length && csv[i + 1] == '\n')
                    {
                        i++;
                        continue;
                    }
                    currentRow.Add(currentField.ToString());
                    currentField.Clear();
                    if (hasContent || currentRow.Count > 1)
                    {
                        rows.Add(currentRow);
                    }
                    currentRow = new List<string>();
                    hasContent = false;
                    i++;
                    continue;
                }

                currentField.Append(c);
                hasContent = true;
                i++;
            }

            // 末尾フィールド・行をフラッシュ
            if (hasContent || currentField.Length > 0 || currentRow.Count > 0)
            {
                currentRow.Add(currentField.ToString());
                rows.Add(currentRow);
            }

            return rows;
        }
    }
}

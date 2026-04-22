using System.Reflection;
using System.Text.RegularExpressions;
using UnityEngine.UIElements;

namespace MitarashiDango.AvatarCatalog
{
    /// <summary>
    /// UXML 内に埋め込まれた @@key@@ プレースホルダを翻訳済み文字列へ置換する
    /// </summary>
    public static class UxmlLocalizer
    {
        // @@key.with.dots@@ 形式のプレースホルダを検出する
        private static readonly Regex KeyPattern = new Regex(@"@@([A-Za-z_][\w\.]*)@@", RegexOptions.Compiled);

        /// <summary>
        /// ツリー内の全要素に対し、翻訳プレースホルダの置換を行う
        /// </summary>
        public static void Apply(VisualElement root)
        {
            if (root == null)
            {
                return;
            }

            foreach (var element in root.Query<VisualElement>().Build())
            {
                // 1. TextElement (Label, Button, Toolbar 文字列等) の text
                if (element is TextElement textElement)
                {
                    textElement.text = Replace(textElement.text);
                }

                // 2. BaseField 系 (TextField, Vector3Field, ObjectField, PropertyField 等) の label
                //    BaseField<T> は generic なので reflection で探す
                TryReplaceStringProperty(element, "label");

                // 3. HelpBox など text プロパティを持つがTextElement継承していないもの
                if (!(element is TextElement))
                {
                    TryReplaceStringProperty(element, "text");
                }

                // 4. tooltip (VisualElement.tooltip) は全要素共通
                if (!string.IsNullOrEmpty(element.tooltip))
                {
                    element.tooltip = Replace(element.tooltip);
                }
            }
        }

        private static void TryReplaceStringProperty(VisualElement element, string propertyName)
        {
            var type = element.GetType();
            var prop = type.GetProperty(
                propertyName,
                BindingFlags.Public | BindingFlags.Instance,
                binder: null,
                returnType: typeof(string),
                types: System.Type.EmptyTypes,
                modifiers: null);

            if (prop == null || !prop.CanRead || !prop.CanWrite)
            {
                return;
            }

            var current = (string)prop.GetValue(element);
            if (string.IsNullOrEmpty(current))
            {
                return;
            }

            var replaced = Replace(current);
            if (!ReferenceEquals(current, replaced) && current != replaced)
            {
                prop.SetValue(element, replaced);
            }
        }

        private static string Replace(string input)
        {
            if (string.IsNullOrEmpty(input))
            {
                return input;
            }
            if (input.IndexOf("@@", System.StringComparison.Ordinal) < 0)
            {
                return input;
            }
            return KeyPattern.Replace(input, m => AcL10n.Tr(m.Groups[1].Value));
        }
    }
}

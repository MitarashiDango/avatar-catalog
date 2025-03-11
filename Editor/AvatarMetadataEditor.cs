using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace MitarashiDango.AvatarCatalog
{
    [CustomEditor(typeof(AvatarMetadata))]
    public class AvatarMetadataEditor : Editor
    {
        private SerializedProperty _description;
        private SerializedProperty _note;
        private SerializedProperty _inUseAssets;

        private ReorderableList reorderableList;

        private void OnEnable()
        {
            _description = serializedObject.FindProperty("description");
            _note = serializedObject.FindProperty("note");
            _inUseAssets = serializedObject.FindProperty("inUseAssets");

            reorderableList = new ReorderableList(serializedObject, _inUseAssets)
            {
                drawElementCallback = (rect, index, active, focused) =>
                {
                    var inUseAsset = _inUseAssets.GetArrayElementAtIndex(index);
                    var position = new Rect(rect)
                    {
                        y = rect.y + EditorGUIUtility.standardVerticalSpacing,
                        height = EditorGUIUtility.singleLineHeight
                    };

                    EditorGUI.ObjectField(position, inUseAsset, GUIContent.none);

                    if (inUseAsset?.objectReferenceValue != null)
                    {
                        var so = new SerializedObject(inUseAsset.objectReferenceValue);
                        var assetName = so.FindProperty("assetName");
                        var assetNameRect = new Rect(position)
                        {
                            y = position.y + position.height + EditorGUIUtility.standardVerticalSpacing,
                            height = EditorGUIUtility.singleLineHeight
                        };

                        var productUrl = so.FindProperty("productUrl");
                        var productUrlRect = new Rect(assetNameRect)
                        {
                            y = assetNameRect.y + assetNameRect.height + EditorGUIUtility.standardVerticalSpacing,
                            height = EditorGUIUtility.singleLineHeight
                        };

                        EditorGUI.BeginDisabledGroup(true);
                        EditorGUI.PropertyField(assetNameRect, assetName, new GUIContent("アセット名"), false);
                        EditorGUI.PropertyField(productUrlRect, productUrl, new GUIContent("URL"), false);
                        EditorGUI.EndDisabledGroup();
                    }
                },
                drawHeaderCallback = (rect) => EditorGUI.LabelField(rect, "使用しているアセット"),
                elementHeightCallback = (index) =>
                {
                    var inUseAsset = _inUseAssets.GetArrayElementAtIndex(index);
                    if (inUseAsset?.objectReferenceValue == null)
                    {
                        return EditorGUIUtility.singleLineHeight;
                    }

                    return EditorGUIUtility.singleLineHeight * 3 + EditorGUIUtility.standardVerticalSpacing * 2;
                }
            };
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.LabelField("説明");
            var newDescription = EditorGUILayout.TextArea(_description.stringValue, GUILayout.MinHeight(EditorGUIUtility.singleLineHeight * 2));
            if (newDescription != _description.stringValue)
            {
                _description.stringValue = newDescription;
            }

            EditorGUILayout.LabelField("メモ");
            var newNote = EditorGUILayout.TextArea(_note.stringValue, GUILayout.MinHeight(EditorGUIUtility.singleLineHeight * 2));
            if (newNote != _note.stringValue)
            {
                _note.stringValue = newDescription;
            }

            reorderableList.DoLayoutList();

            if (EditorApplication.isPlaying)
            {
                return;
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}

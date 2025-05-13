using MitarashiDango.AvatarCatalog.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace MitarashiDango.AvatarCatalog
{
    [CustomEditor(typeof(AvatarThumbnailSettings))]
    public class AvatarThumbnailSettingsEditor : Editor
    {
        // UXML要素のクエリ名定義
        private const string CameraPositionOffsetFieldName = "camera-position-offset-field";
        private const string CameraRotationFieldName = "camera-rotation-field";
        private const string ShowThumbnailPreviewWindowButtonName = "show-thumbnail-preview-window-button";

        [SerializeField]
        private VisualTreeAsset _mainUxmlAsset;

        private Vector3Field _cameraRotationField;
        private SerializedProperty _cameraPositionOffset;
        private SerializedProperty _cameraRotation;

        private void OnEnable()
        {
            // イベント登録
            EditorSceneManager.sceneOpened -= OnSceneOpened; // 念のため一度解除
            EditorSceneManager.sceneOpened += OnSceneOpened;

            // SerializedPropertyを初期化
            _cameraPositionOffset = serializedObject.FindProperty("cameraPositionOffset");
            _cameraRotation = serializedObject.FindProperty("cameraRotation");
        }

        private void OnDestroy()
        {
            // イベント解除
            EditorSceneManager.sceneOpened -= OnSceneOpened;

            // Undo/Redoコールバックの解除 (もし_cameraRotationFieldがnullでなければ)
            // CreateInspectorGUIが呼ばれる前にOnDestroyが呼ばれるケースを考慮
            if (_cameraRotationField != null)
            {
                Undo.undoRedoPerformed -= UndoRedoCallback;
            }
        }

        public override VisualElement CreateInspectorGUI()
        {
            if (_mainUxmlAsset == null)
            {
                return new Label("Main UXML Asset is not assigned.");
            }

            var root = _mainUxmlAsset.CloneTree();

            ApplyCustomFont(root);
            SetupCameraPositionOffsetField(root);
            SetupCameraRotationField(root);
            SetupShowThumbnailPreviewWindowButton(root);

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

        /// <summary>
        /// カメラ位置オフセットフィールドを設定します
        /// </summary>
        private void SetupCameraPositionOffsetField(VisualElement root)
        {
            var cameraPositionOffsetField = root.Q<Vector3Field>(CameraPositionOffsetFieldName);
            if (cameraPositionOffsetField != null)
            {
                Debug.LogWarning($"{CameraPositionOffsetFieldName} not found in UXML.");
                return;
            }

            cameraPositionOffsetField.BindProperty(_cameraPositionOffset);
        }

        /// <summary>
        /// カメラ回転フィールドを設定します
        /// </summary>
        private void SetupCameraRotationField(VisualElement root)
        {
            _cameraRotationField = root.Q<Vector3Field>(CameraRotationFieldName);
            if (_cameraRotationField == null)
            {
                Debug.LogWarning($"{CameraRotationFieldName} not found in UXML.");
                return;
            }

            // 初期値を設定 (QuaternionからEulerへ)
            _cameraRotationField.value = _cameraRotation.quaternionValue.eulerAngles;

            // 値変更時のコールバック
            _cameraRotationField.RegisterValueChangedCallback(evt =>
            {
                _cameraRotation.quaternionValue = Quaternion.Euler(evt.newValue);
                _cameraRotation.serializedObject.ApplyModifiedProperties();
            });

            // Undo/Redo処理のためのコールバック登録
            _cameraRotationField.RegisterCallback<AttachToPanelEvent>(OnAttachToPanel);
            _cameraRotationField.RegisterCallback<DetachFromPanelEvent>(OnDetachFromPanel);
        }

        /// <summary>
        /// サムネイルプレビューウィンドウ表示ボタンを設定します
        /// </summary>
        private void SetupShowThumbnailPreviewWindowButton(VisualElement root)
        {
            var showThumbnailPreviewWindowButton = root.Q<Button>(ShowThumbnailPreviewWindowButtonName);
            if (showThumbnailPreviewWindowButton == null)
            {
                Debug.LogWarning($"{ShowThumbnailPreviewWindowButtonName} not found in UXML.");
                return;
            }

            showThumbnailPreviewWindowButton.clicked += OnShowThumbnailPreviewWindowButtonClick;
        }

        private void OnSceneOpened(Scene scene, OpenSceneMode mode)
        {
            // シーンが変更された際にインスペクターを再描画する
            Repaint();
        }

        private void OnShowThumbnailPreviewWindowButtonClick()
        {
            if (target is AvatarThumbnailSettings avatarThumbnailSettings && avatarThumbnailSettings.gameObject != null)
            {
                AvatarThumbnailPreviewWindow.ShowWindow(avatarThumbnailSettings.gameObject);
            }
        }

        /// <summary>
        /// UI要素がパネルにアタッチされたときに呼び出されます<br />
        /// Undo/Redoコールバックを登録し、初期状態をUIに反映します
        /// </summary>
        private void OnAttachToPanel(AttachToPanelEvent evt)
        {
            Undo.undoRedoPerformed -= UndoRedoCallback; // 念のため一度解除
            Undo.undoRedoPerformed += UndoRedoCallback;
            UndoRedoCallback(); // 初期状態を反映
        }

        /// <summary>
        /// UI要素がパネルからデタッチされたときに呼び出されます<br />
        /// Undo/Redoコールバックを解除します
        /// </summary>
        private void OnDetachFromPanel(DetachFromPanelEvent evt)
        {
            Undo.undoRedoPerformed -= UndoRedoCallback;
        }

        /// <summary>
        /// Undo/Redo操作が行われたときに呼び出されるコールバック<br />
        /// SerializedObjectを更新し、UIフィールドに値を再設定します
        /// </summary>
        private void UndoRedoCallback()
        {
            if (_cameraRotation == null || _cameraRotationField == null)
            {
                return;
            }

            // SerializedObjectの状態を最新に更新
            _cameraRotation.serializedObject.Update();

            // UIフィールドに値を通知なしで設定 (これによりValueChangedCallbackがトリガーされるのを防ぐ)
            _cameraRotationField.SetValueWithoutNotify(_cameraRotation.quaternionValue.eulerAngles);
        }
    }
}
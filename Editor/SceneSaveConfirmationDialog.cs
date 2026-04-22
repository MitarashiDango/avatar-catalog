using UnityEditor;
using UnityEngine;

namespace MitarashiDango.AvatarCatalog
{
    public enum SceneSaveChoice
    {
        Save,
        Skip,
    }

    /// <summary>
    /// 一時的に開いたシーンが dirty となった場合に、保存するかどうかをユーザーへ確認するためのダイアログ
    /// </summary>
    public static class SceneSaveConfirmationDialog
    {
        private const string SessionKey = "MitarashiDango.AvatarCatalog.SceneSaveConfirmation.RememberedChoice";
        private const string SessionValueSave = "save";
        private const string SessionValueSkip = "skip";

        public static SceneSaveChoice Prompt(string sceneName)
        {
            var remembered = SessionState.GetString(SessionKey, "");
            if (remembered == SessionValueSave)
            {
                return SceneSaveChoice.Save;
            }
            if (remembered == SessionValueSkip)
            {
                return SceneSaveChoice.Skip;
            }

            var (choice, remember) = SceneSaveConfirmationWindow.Open(sceneName);
            if (remember)
            {
                SessionState.SetString(SessionKey, choice == SceneSaveChoice.Save ? SessionValueSave : SessionValueSkip);
            }

            return choice;
        }

        /// <summary>
        /// セッション内で記憶された選択をリセットする
        /// </summary>
        public static void ResetSessionChoice()
        {
            SessionState.EraseString(SessionKey);
        }
    }

    internal class SceneSaveConfirmationWindow : EditorWindow
    {
        private string _sceneName;
        private bool _remember;

        private SceneSaveChoice _result = SceneSaveChoice.Skip;
        private bool _rememberResult;

        public static (SceneSaveChoice choice, bool remember) Open(string sceneName)
        {
            var window = CreateInstance<SceneSaveConfirmationWindow>();
            window.titleContent = new GUIContent(AcL10n.Tr("scene_save_dialog.title"));
            window._sceneName = sceneName;
            window.minSize = new Vector2(440, 170);
            window.maxSize = new Vector2(440, 170);
            window.ShowModal();
            return (window._result, window._rememberResult);
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField(
                AcL10n.Tr("scene_save_dialog.message1", _sceneName),
                EditorStyles.wordWrappedLabel);
            EditorGUILayout.LabelField(
                AcL10n.Tr("scene_save_dialog.message2"),
                EditorStyles.wordWrappedLabel);
            EditorGUILayout.Space(8);

            _remember = EditorGUILayout.ToggleLeft(
                AcL10n.Tr("scene_save_dialog.remember_choice"),
                _remember);

            EditorGUILayout.Space(10);

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                if (GUILayout.Button(AcL10n.Tr("scene_save_dialog.save"), GUILayout.MinWidth(100), GUILayout.Height(24)))
                {
                    _result = SceneSaveChoice.Save;
                    _rememberResult = _remember;
                    Close();
                }
                if (GUILayout.Button(AcL10n.Tr("scene_save_dialog.dont_save"), GUILayout.MinWidth(100), GUILayout.Height(24)))
                {
                    _result = SceneSaveChoice.Skip;
                    _rememberResult = _remember;
                    Close();
                }
            }
        }
    }
}

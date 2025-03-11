using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using VRC.SDK3.Avatars.Components;

namespace MitarashiDango.AvatarCatalog
{
    public class AvatarCatalogWindow : EditorWindow
    {
        private AvatarRenderer _avatarRenderer;
        private List<AvatarListItem> _avatarListItems = new List<AvatarListItem>();
        private int imageSize = 192;
        private int padding = 10;
        private int columns = 3;
        private Vector2 scrollPosition;

        [MenuItem("Tools/Avatar Catalog/Avatar List")]
        public static void ShowWindow()
        {
            var window = GetWindow<AvatarCatalogWindow>("Avatar List");
            window.minSize = new Vector2(800, 600);
        }

        private void OnEnable()
        {
            RefreshAvatars();
        }

        private void OnDisable()
        {
            _avatarRenderer?.Dispose();
            _avatarRenderer = null;
        }

        private void InitializeAvatarRenderer()
        {
            if (_avatarRenderer != null)
            {
                return;
            }

            _avatarRenderer = new AvatarRenderer();
        }

        private void RefreshAvatars()
        {
            var rootObjects = SceneManager.GetActiveScene().GetRootGameObjects();

            _avatarListItems.Clear();
            _avatarListItems.AddRange(rootObjects.Where(o => o != null && o.GetComponent<VRCAvatarDescriptor>() != null).Select(avatar => new AvatarListItem(avatar)));

            // TODO 設定で調整可能にする
            // MEMO 設定の持たせ方は要検討
            var xOffset = 0f;
            var yOffset = -0.5f;
            var zOffset = 5.2f;
            var backgroundColor = Color.white;

            InitializeAvatarRenderer();

            // TODO 画像をキャッシュするようにする
            foreach (var avatarListItem in _avatarListItems)
            {
                var avatarDescriptor = avatarListItem.Avatar.GetComponent<VRCAvatarDescriptor>();
                var cameraPosition = new Vector3(xOffset, avatarDescriptor.ViewPosition.y + yOffset, avatarDescriptor.ViewPosition.z + zOffset);

                _avatarRenderer.CameraPosition = cameraPosition;
                _avatarRenderer.CameraRotation = Quaternion.Euler(0, 180, 0);
                _avatarRenderer.CameraScale = new Vector3(1, 1, 1);

                avatarListItem.Thumbnail = _avatarRenderer.Render(avatarListItem.Avatar, 256, 256, null, null, false);
            }
        }

        private void OnGUI()
        {
            if (_avatarListItems.Count == 0)
            {
                EditorGUILayout.LabelField("No avatars found.");
                return;
            }

            // ウィンドウ幅に基づいて列数を決定（最大サイズ192x192）
            columns = Mathf.Max(1, (int)(position.width / (imageSize + padding)));
            imageSize = Mathf.Min(192, (int)(position.width / columns) - padding); // サイズ制限

            int rows = Mathf.CeilToInt((float)_avatarListItems.Count / columns);

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            for (int row = 0; row < rows; row++)
            {
                EditorGUILayout.BeginHorizontal();

                for (int col = 0; col < columns; col++)
                {
                    int index = row * columns + col;
                    if (index >= _avatarListItems.Count)
                    {
                        break;
                    }

                    // 画像＋テキストを1つのボタンとしてラップ
                    if (GUILayout.Button("", GUILayout.Width(imageSize), GUILayout.Height(imageSize + 20)))
                    {
                        Debug.Log("Selected: " + _avatarListItems[index].Avatar.name);
                        for (var i = 0; i < _avatarListItems.Count; i++)
                        {
                            if (index == i)
                            {
                                _avatarListItems[i].Avatar.SetActive(true);
                            }
                            else if (_avatarListItems[i].Avatar.activeSelf)
                            {
                                _avatarListItems[i].Avatar.SetActive(false);
                            }
                        }
                    }

                    Rect lastRect = GUILayoutUtility.GetLastRect();
                    GUI.DrawTexture(new Rect(lastRect.x, lastRect.y, imageSize, imageSize), _avatarListItems[index].Thumbnail, ScaleMode.ScaleToFit);

                    GUIStyle labelStyle = new GUIStyle(GUI.skin.label)
                    {
                        alignment = TextAnchor.MiddleCenter,
                        wordWrap = true
                    };

                    GUI.Label(new Rect(lastRect.x, lastRect.y + imageSize, imageSize, 20), _avatarListItems[index].Avatar.name, labelStyle);
                }

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();

            if (GUILayout.Button("Reload Avatars"))
            {
                RefreshAvatars();
                Repaint();
            }
        }

        internal class AvatarListItem
        {
            public GameObject Avatar { get; set; }
            public Texture2D Thumbnail { get; set; }

            public AvatarListItem(GameObject avatar)
            {
                Avatar = avatar;
            }
        }
    }
}

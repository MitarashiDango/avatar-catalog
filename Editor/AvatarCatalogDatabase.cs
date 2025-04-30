using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MitarashiDango.AvatarCatalog.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VRC.SDK3.Avatars.Components;

namespace MitarashiDango.AvatarCatalog
{
    [Serializable]
    public class AvatarCatalogDatabase : ScriptableObject
    {
        private static readonly float XOffset = 0f;
        private static readonly float YOffset = 0f;
        private static readonly float ZOffset = 0f;

        private static readonly Color BackgroundColor = Color.clear;
        private static readonly int ThumbnailImageSize = 512;
        public static readonly string AssetFilePath = "Assets/Avatar Catalog User Data/AvatarCatalogDatabase.asset";

        [SerializeField]
        private List<AvatarCatalogEntry> _avatars = new List<AvatarCatalogEntry>();

        public List<AvatarCatalogEntry> avatars
        {
            get => _avatars;
            private set => _avatars = value;
        }

        public bool IsExists(GameObject avatar)
        {
            return IsExists(GlobalObjectId.GetGlobalObjectIdSlow(avatar));
        }

        public bool IsExists(GlobalObjectId avatarGlobalObjectId)
        {
            var gid = avatarGlobalObjectId.ToString();
            return _avatars.Where(a => a.avatarGlobalObjectId == gid).Count() > 0;
        }

        public Texture2D TryGetCachedAvatarThumbnailImage(GlobalObjectId avatarGlobalObjectId)
        {
            var gid = avatarGlobalObjectId.ToString();
            var avatar = avatars.First(t => t.avatarGlobalObjectId == gid);
            if (avatar == null)
            {
                return null;
            }

            var path = AssetDatabase.GUIDToAssetPath(avatar.thumbnailImageGuid);
            if (path == "")
            {
                return null;
            }

            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        public Dictionary<string, AvatarCatalogEntry> GetMappedAvatarCatalogEntries()
        {
            var dic = new Dictionary<string, AvatarCatalogEntry>();

            foreach (var entry in avatars)
            {
                dic.Add(entry.avatarGlobalObjectId, entry);
            }

            return dic;
        }

        /// <summary>
        /// アバターカタログ情報を更新する
        /// </summary>
        /// <param name="withRefreshThumbnail">更新時にサムネイル画像も新しくするか</param> <summary>
        /// 
        /// </summary>
        /// <param name="withRefreshThumbnail"></param>
        public void RefreshAvatarCatalog(bool withRefreshThumbnail = false)
        {
            // フォルダー作成
            FolderUtil.CreateUserDataFolders();

            var prevAvatars = GetMappedAvatarCatalogEntries();
            var newAvatars = new List<AvatarCatalogEntry>();
            var avatarRenderer = new AvatarRenderer();

            try
            {
                var scenes = GetAllScenes();
                for (var i = 0; i < scenes.Count; i++)
                {
                    var scenePath = AssetDatabase.GetAssetPath(scenes[i]);
                    var currentScene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);

                    var currentSceneRootObjects = currentScene.GetRootGameObjects();
                    var avatarObjects = currentSceneRootObjects.Where(o => o != null && o.GetComponent<VRCAvatarDescriptor>() != null);

                    foreach (var avatarObject in avatarObjects)
                    {
                        var avatarDescriptor = avatarObject.GetComponent<VRCAvatarDescriptor>();
                        var avatarGlobalObjectId = GlobalObjectId.GetGlobalObjectIdSlow(avatarObject);
                        var avatarGlobalObjectIdString = avatarGlobalObjectId.ToString();

                        if (!prevAvatars.ContainsKey(avatarGlobalObjectIdString))
                        {
                            // 未追加のアバター
                            var thumbnail = RenderAvatarThumbnail(avatarRenderer, avatarObject);

                            newAvatars.Add(new AvatarCatalogEntry()
                            {
                                avatarGlobalObjectId = avatarGlobalObjectIdString,
                                avatarObjectName = avatarObject.name,
                                sceneAsset = scenes[i],
                                thumbnailImageGuid = StoreAvatarThumbnailImage(avatarGlobalObjectId, thumbnail).ToString(),
                            });
                        }
                        else
                        {
                            // 既知のアバター情報の更新
                            var avatar = new AvatarCatalogEntry(prevAvatars[avatarGlobalObjectIdString]);
                            avatar.avatarObjectName = avatarObject.name;

                            if (avatar.thumbnailImageGuid == "" || AssetDatabase.GUIDToAssetPath(avatar.thumbnailImageGuid) == "" || withRefreshThumbnail)
                            {
                                var thumbnail = RenderAvatarThumbnail(avatarRenderer, avatarObject);
                                avatar.thumbnailImageGuid = StoreAvatarThumbnailImage(avatarGlobalObjectId, thumbnail).ToString();
                                newAvatars.Add(avatar);
                            }
                            else
                            {
                                newAvatars.Add(avatar);
                            }
                        }
                    }

                    if (currentScene != EditorSceneManager.GetActiveScene())
                    {
                        EditorSceneManager.CloseScene(currentScene, true);
                    }
                }
            }
            catch (Exception e)
            {
                Debug.Log(e);
                return;
            }
            finally
            {
                avatarRenderer.Dispose();
                avatarRenderer = null;
            }

            // 不要となったファイルの削除
            CleanupFiles(prevAvatars, newAvatars);

            avatars = newAvatars;

            Save();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        public void UpdateAvatarThumbnail(AvatarCatalogEntry avatar)
        {
            var scenePath = AssetDatabase.GetAssetPath(avatar.sceneAsset);
            var scene = SceneManager.GetSceneByPath(scenePath);

            var avatarRenderer = new AvatarRenderer();

            try
            {
                if (!scene.isLoaded)
                {
                    if (!EditorSceneManager.SaveOpenScenes())
                    {
                        Debug.Log("failed to save open scene");
                        return;
                    }

                    scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
                }

                if (!GlobalObjectId.TryParse(avatar.avatarGlobalObjectId, out var avatarGlobalObjectId))
                {
                    Debug.LogWarning("Failed to parse GlobalObjectId");
                    return;
                }

                var targetAvatarObject = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(avatarGlobalObjectId) as GameObject;
                if (targetAvatarObject == null)
                {
                    Debug.Log("failed to find avatar object");
                    return;
                }

                var avatarDescriptor = targetAvatarObject.GetComponent<VRCAvatarDescriptor>();
                if (avatarDescriptor == null)
                {
                    Debug.Log("failed to find VRCAvatarDescriptor component");
                    return;
                }

                var index = avatars.FindIndex(t => t.avatarGlobalObjectId == avatar.avatarGlobalObjectId);
                if (index >= 0)
                {
                    var thumbnail = RenderAvatarThumbnail(avatarRenderer, targetAvatarObject);
                    avatars[index].thumbnailImageGuid = StoreAvatarThumbnailImage(avatarGlobalObjectId, thumbnail).ToString();
                }
            }
            finally
            {
                avatarRenderer.Dispose();
                avatarRenderer = null;

                if (scene != EditorSceneManager.GetActiveScene())
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        public void Save(bool withSaveAssets = false)
        {
            var asset = AssetDatabase.LoadAssetAtPath<AvatarCatalogDatabase>(AssetFilePath);
            if (asset == null)
            {
                // フォルダー作成
                FolderUtil.CreateUserDataFolder();

                AssetDatabase.CreateAsset(this, AssetFilePath);
            }
            else
            {
                EditorUtility.CopySerialized(this, asset);
                EditorUtility.SetDirty(asset);
            }

            if (withSaveAssets)
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
        }

        private static void CleanupFiles(Dictionary<string, AvatarCatalogEntry> prevAvatars, List<AvatarCatalogEntry> newAvatars)
        {
            var newAvatarGlobalObjectIds = newAvatars.Select(avatar => avatar.avatarGlobalObjectId);
            var removedAvatars = prevAvatars.Values.Where(prevAvatar => !newAvatarGlobalObjectIds.Contains(prevAvatar.avatarGlobalObjectId));
            foreach (var removedAvatar in removedAvatars)
            {
                // サムネイル画像の削除
                var thumbnailImagePath = AssetDatabase.GUIDToAssetPath(new GUID(removedAvatar.thumbnailImageGuid));
                if (thumbnailImagePath != "")
                {
                    AssetDatabase.DeleteAsset(thumbnailImagePath);
                }

                if (GlobalObjectId.TryParse(removedAvatar.avatarGlobalObjectId, out var avatarGlobalObjectId))
                {
                    // アバターメタデータの削除
                    var avatarMetadataPath = AvatarMetadataUtil.GetMetadataPath(avatarGlobalObjectId);
                    var guid = AssetDatabase.GUIDFromAssetPath(avatarMetadataPath);
                    if (!guid.Empty())
                    {
                        AssetDatabase.DeleteAsset(avatarMetadataPath);
                    }
                }
            }
        }

        private static Texture2D RenderAvatarThumbnail(AvatarRenderer avatarRenderer, GameObject avatarObject)
        {
            return avatarRenderer.Render(avatarObject, GetCameraSetting(avatarObject), ThumbnailImageSize, ThumbnailImageSize, null, null, false);
        }

        public static GUID StoreAvatarThumbnailImage(GlobalObjectId avatarGlobalObjectId, Texture2D texture)
        {
            var avatarThumbnailImagePath = $"{FolderUtil.AvatarThumbnailCacheFolderPath}/thumbnail_{avatarGlobalObjectId.assetGUID}_{avatarGlobalObjectId.targetObjectId}_{avatarGlobalObjectId.targetPrefabId}.png";

            var existingThumbnailImageGuid = AssetDatabase.GUIDFromAssetPath(avatarThumbnailImagePath);
            if (!existingThumbnailImageGuid.Empty())
            {
                // ファイルが存在する場合、上書きする
                File.WriteAllBytes(avatarThumbnailImagePath, texture.EncodeToPNG());
                return existingThumbnailImageGuid;
            }

            // ファイルを新規作成する
            File.WriteAllBytes(avatarThumbnailImagePath, texture.EncodeToPNG());
            AssetDatabase.ImportAsset(avatarThumbnailImagePath);

            return AssetDatabase.GUIDFromAssetPath(avatarThumbnailImagePath);
        }

        private static List<SceneAsset> GetAllScenes()
        {
            return AssetDatabase.FindAssets("t:SceneAsset", new[] { "Assets" })
                .Select(guid => AssetDatabase.GUIDToAssetPath(guid))
                .Select(path => AssetDatabase.LoadAssetAtPath<SceneAsset>(path))
                .Where(asset => asset != null)
                .ToList();
        }

        private static AvatarRenderer.CameraSetting GetCameraSetting(GameObject avatarObject)
        {
            var avatarCatalogThumbnailSettings = avatarObject.GetComponent<AvatarCatalogThumbnailSettings>();

            var cameraSetting = new AvatarRenderer.CameraSetting();
            cameraSetting.BackgroundColor = BackgroundColor;
            cameraSetting.PositionOffset = avatarCatalogThumbnailSettings != null && avatarCatalogThumbnailSettings.cameraPositionOffset != null ? avatarCatalogThumbnailSettings.cameraPositionOffset : new Vector3(XOffset, YOffset, ZOffset);
            cameraSetting.Rotation = avatarCatalogThumbnailSettings != null && avatarCatalogThumbnailSettings.cameraRotation != null ? avatarCatalogThumbnailSettings.cameraRotation : Quaternion.Euler(0, 180, 0);
            cameraSetting.Scale = new Vector3(1, 1, 1);

            return cameraSetting;
        }

        public static AvatarCatalogDatabase LoadOrNewInstance()
        {
            var asset = Load();

            if (asset != null)
            {
                return asset;
            }

            return CreateInstance<AvatarCatalogDatabase>();
        }

        public static AvatarCatalogDatabase LoadOrCreateFile()
        {
            var asset = Load();

            if (asset != null)
            {
                return asset;
            }

            // フォルダー作成
            FolderUtil.CreateUserDataFolder();

            asset = CreateInstance<AvatarCatalogDatabase>();
            AssetDatabase.CreateAsset(asset, AssetFilePath);

            return asset;
        }

        public static AvatarCatalogDatabase Load()
        {
            var asset = AssetDatabase.LoadAssetAtPath<AvatarCatalogDatabase>(AssetFilePath);
            if (asset != null)
            {
                return asset;
            }

            return null;
        }

        public static bool IsDatabaseFileExists()
        {
            return AssetDatabase.LoadAssetAtPath<AvatarCatalogDatabase>(AssetFilePath) != null;
        }

        [Serializable]
        public class AvatarCatalogEntry
        {
            public string avatarGlobalObjectId;
            public string avatarObjectName;
            public SceneAsset sceneAsset;
            public string thumbnailImageGuid = "";

            public AvatarCatalogEntry()
            {
            }

            public AvatarCatalogEntry(AvatarCatalogEntry ace)
            {
                avatarGlobalObjectId = ace.avatarGlobalObjectId;
                avatarObjectName = ace.avatarObjectName;
                sceneAsset = ace.sceneAsset;
                thumbnailImageGuid = ace.thumbnailImageGuid;
            }
        }
    }
}

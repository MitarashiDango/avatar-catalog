using System;
using System.Collections.Generic;
using System.Linq;
using MitarashiDango.AvatarCatalog.Runtime;
using UnityEditor;
using VRC.SDK3.Avatars.Components;

namespace MitarashiDango.AvatarCatalog
{
    public class DatabaseBuilder
    {
        /// <summary>
        /// アバターカタログデータベースと各種インデックスを構築する
        /// </summary>
        /// <param name="withRefreshThumbnail">更新時にサムネイル画像も新しくするか</param>
        public static void BuildAvatarCatalogDatabaseAndIndexes(bool withRefreshThumbnail = false)
        {
            // フォルダー作成
            FolderUtil.CreateUserDataFolders();
            FolderUtil.CreateCacheFolder();
            FolderUtil.CreateAvatarThumbnailsCacheFolder();

            var avatarCatalogDatabase = AvatarCatalogDatabase.LoadOrCreateFile();

            var prevAvatars = avatarCatalogDatabase.GetMappedAvatarCatalogEntries();
            var newAvatars = new List<AvatarCatalogDatabase.AvatarCatalogEntry>();

            using var avatarRenderer = new AvatarRenderer();

            MiscUtil.WalkAllScenes((sceneAsset, currentScene) =>
            {
                var currentSceneRootObjects = currentScene.GetRootGameObjects();
                var avatarObjects = currentSceneRootObjects.Where(o => o != null && o.GetComponent<VRCAvatarDescriptor>() != null);

                foreach (var avatarObject in avatarObjects)
                {
                    var avatarDescriptor = avatarObject.GetComponent<VRCAvatarDescriptor>();
                    var avatarGlobalObjectId = GlobalObjectId.GetGlobalObjectIdSlow(avatarObject);
                    var avatarGlobalObjectIdString = avatarGlobalObjectId.ToString();

                    // マイグレーション処理
                    AvatarMetadataUtil.MigrateAvatarMetadataSettings(avatarObject);

                    var avatarMetadataSettings = avatarObject.GetComponent<AvatarMetadataSettings>();
                    var avatarMetadataGuid = "";
                    if (avatarMetadataSettings != null && avatarMetadataSettings.avatarMetadata != null)
                    {
                        var avatarMetadataFilePath = AssetDatabase.GetAssetPath(avatarMetadataSettings.avatarMetadata);
                        if (!string.IsNullOrEmpty(avatarMetadataFilePath))
                        {
                            avatarMetadataGuid = AssetDatabase.AssetPathToGUID(avatarMetadataFilePath);
                        }
                    }

                    if (!prevAvatars.ContainsKey(avatarGlobalObjectIdString))
                    {
                        // 未追加のアバター
                        var thumbnail = AvatarThumbnailUtil.RenderAvatarThumbnail(avatarRenderer, avatarObject);

                        newAvatars.Add(new AvatarCatalogDatabase.AvatarCatalogEntry()
                        {
                            avatarGlobalObjectId = avatarGlobalObjectIdString,
                            avatarObjectName = avatarObject.name,
                            sceneAsset = sceneAsset,
                            thumbnailImageGuid = AvatarThumbnailUtil.StoreAvatarThumbnailImage(thumbnail, avatarObject).ToString(),
                            avatarMetadataGuid = avatarMetadataGuid,
                        });
                    }
                    else
                    {
                        // 既知のアバター情報の更新
                        var avatar = new AvatarCatalogDatabase.AvatarCatalogEntry(prevAvatars[avatarGlobalObjectIdString]);
                        avatar.avatarObjectName = avatarObject.name;
                        avatar.avatarMetadataGuid = avatarMetadataGuid;

                        var avatarThumbnailImageExists = string.IsNullOrEmpty(avatar.thumbnailImageGuid) && !string.IsNullOrEmpty(AssetDatabase.GUIDToAssetPath(avatar.thumbnailImageGuid));

                        if (avatarThumbnailImageExists)
                        {
                            // 既存のアバターサムネイル画像に対する処理
                            if (GUID.TryParse(avatar.thumbnailImageGuid, out var thumbnailImageGuid))
                            {
                                if (withRefreshThumbnail)
                                {
                                    // 古いサムネイル画像を削除
                                    AvatarThumbnailUtil.DeleteAvatarThumbnailImage(thumbnailImageGuid);
                                }
                                else
                                {
                                    // サムネイル画像のファイル名を更新する
                                    AvatarThumbnailUtil.RenameAvatarThumbnailImage(thumbnailImageGuid, avatarObject);
                                }
                            }
                        }

                        if (!avatarThumbnailImageExists || withRefreshThumbnail)
                        {
                            // サムネイル画像を新規作成または更新する
                            var thumbnail = AvatarThumbnailUtil.RenderAvatarThumbnail(avatarRenderer, avatarObject);
                            avatar.thumbnailImageGuid = AvatarThumbnailUtil.StoreAvatarThumbnailImage(thumbnail, avatarObject).ToString();
                        }

                        newAvatars.Add(avatar);
                    }
                }
            });

            // 不要となったファイルの削除
            CleanupFiles(prevAvatars, newAvatars);

            avatarCatalogDatabase.avatars = newAvatars;

            AvatarCatalogDatabase.Save(avatarCatalogDatabase);

            // 検索インデックスの最新化
            RefreshIndexes();

            AssetDatabase.Refresh();
        }

        private static void RefreshIndexes()
        {
            var avatarCatalogDatabase = AvatarCatalogDatabase.LoadOrCreateFile();
            var avatarSearchIndex = AvatarSearchIndex.LoadOrCreateFile();

            avatarSearchIndex.entries = avatarCatalogDatabase.avatars.Select(avatar =>
            {
                var avatarSearchIndexEntiry = new AvatarSearchIndex.AvatarSearchIndexEntry();
                avatarSearchIndexEntiry.avatarGlobalObjectId = avatar.avatarGlobalObjectId;
                avatarSearchIndexEntiry.Values = GenerateAvatarSearchIndexWords(avatar);
                return avatarSearchIndexEntiry;
            }).ToList();

            AvatarSearchIndex.Save(avatarSearchIndex);
        }

        public static void RefreshIndexes(GlobalObjectId avatarGlobalObjectId)
        {
            RefreshIndexes(avatarGlobalObjectId.ToString());
        }

        public static void RefreshIndexes(string avatarGlobalObjectId)
        {
            var avatarCatalogDatabase = AvatarCatalogDatabase.LoadOrCreateFile();
            var avatarSearchIndex = AvatarSearchIndex.LoadOrCreateFile();

            var avatarCatalogDatabaseEntity = avatarCatalogDatabase.Get(avatarGlobalObjectId);
            if (avatarCatalogDatabaseEntity == null)
            {
                return;
            }

            var avatarSearchIndexEntiry = new AvatarSearchIndex.AvatarSearchIndexEntry();
            avatarSearchIndexEntiry.avatarGlobalObjectId = avatarCatalogDatabaseEntity.avatarGlobalObjectId;
            avatarSearchIndexEntiry.Values = GenerateAvatarSearchIndexWords(avatarCatalogDatabaseEntity);
            avatarSearchIndex.Set(avatarSearchIndexEntiry);

            AvatarSearchIndex.Save(avatarSearchIndex);
        }

        private static List<string> GenerateAvatarSearchIndexWords(AvatarCatalogDatabase.AvatarCatalogEntry avatar)
        {
            var words = new List<string>
            {
                avatar.avatarObjectName,
            };

            if (!string.IsNullOrEmpty(avatar.avatarMetadataGuid) && GUID.TryParse(avatar.avatarMetadataGuid, out var avatarMetadataGuid))
            {
                var avatarMetadata = AvatarMetadataUtil.LoadMetadata(avatarMetadataGuid);
                if (avatarMetadata != null)
                {
                    words.Add(avatarMetadata.comment);
                    words.AddRange(avatarMetadata.tags);

                    var assetProductDetails = avatarMetadata.assetProductDetails.Where(assetProductDetail => assetProductDetail != null).Distinct();
                    foreach (var assetProductDetail in assetProductDetails)
                    {
                        words.Add(assetProductDetail.productName);
                        words.Add(assetProductDetail.creatorName);
                        var tags = assetProductDetail.tags.Where(tag => !string.IsNullOrEmpty(tag)).ToList();
                        if (tags.Count > 0)
                        {
                            words.AddRange(tags);
                        }
                    }
                }
            }

            return words.Distinct().ToList();
        }

        private static void CleanupFiles(Dictionary<string, AvatarCatalogDatabase.AvatarCatalogEntry> prevAvatars, List<AvatarCatalogDatabase.AvatarCatalogEntry> newAvatars)
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
            }
        }
    }
}

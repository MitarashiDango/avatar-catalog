using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MitarashiDango.AvatarCatalog.Runtime;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace MitarashiDango.AvatarCatalog
{
    public class DatabaseBuilder
    {
        public DatabaseBuilder()
        {
        }

        /// <summary>
        /// アバターカタログデータベースと各種インデックスを構築する
        /// </summary>
        /// <param name="withRegenerateThumbnails">更新時にサムネイル画像も新しくするか</param>
        public void BuildAvatarCatalogDatabaseAndIndexes(bool withRegenerateThumbnails = false)
        {
            // フォルダー作成
            FolderUtil.CreateUserDataFolders();
            FolderUtil.CreateCacheFolder();
            FolderUtil.CreateAvatarThumbnailsCacheFolder();

            var avatarCatalogDatabase = AvatarCatalogDatabase.LoadOrCreateFile();

            var prevAvatars = avatarCatalogDatabase.GetMappedAvatarCatalogEntries();
            var newAvatars = new List<AvatarCatalogDatabase.AvatarCatalogEntry>();

            var allAssetProductDetailWithFolderPaths = GetAllAssetProductDetails()
                .Select(apd =>
                {
                    var folderPath = string.IsNullOrEmpty(apd.rootFolderPath)
                        ? Path.GetDirectoryName(AssetDatabase.GetAssetPath(apd))
                        : apd.rootFolderPath;
                    return (assetProductDetail: apd, folderPath: folderPath.Replace("\\", "/"));
                })
                .ToList();
            var assetProductDetails = new Dictionary<string, List<AssetProductDetail>>();

            using var avatarRenderer = new AvatarRenderer();

            SceneProcessor.WalkAllScenes((sceneAsset, currentScene) =>
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

                    var referencedAssetProductDetails = GetReferencedAssetProductDetails(avatarObject, allAssetProductDetailWithFolderPaths).ToList();
                    if (referencedAssetProductDetails.Count > 0)
                    {
                        assetProductDetails.Add(avatarGlobalObjectId.ToString(), referencedAssetProductDetails);
                    }

                    if (!prevAvatars.ContainsKey(avatarGlobalObjectIdString))
                    {
                        // 未追加のアバター
                        var thumbnail = AvatarThumbnailUtil.RenderAvatarThumbnail(avatarRenderer, avatarObject);
                        try
                        {
                            newAvatars.Add(new AvatarCatalogDatabase.AvatarCatalogEntry()
                            {
                                avatarGlobalObjectId = avatarGlobalObjectIdString,
                                avatarObjectName = avatarObject.name,
                                sceneAsset = sceneAsset,
                                thumbnailImageGuid = AvatarThumbnailUtil.StoreAvatarThumbnailImage(thumbnail).ToString(),
                                avatarMetadataGuid = avatarMetadataGuid,
                            });
                        }
                        finally
                        {
                            UnityEngine.Object.DestroyImmediate(thumbnail);
                        }
                    }
                    else
                    {
                        // 既知のアバター情報の更新
                        var avatar = new AvatarCatalogDatabase.AvatarCatalogEntry(prevAvatars[avatarGlobalObjectIdString]);
                        avatar.avatarObjectName = avatarObject.name;
                        avatar.avatarMetadataGuid = avatarMetadataGuid;

                        var avatarThumbnailImageExists = IsAssetExists(avatar.thumbnailImageGuid);

                        if (avatarThumbnailImageExists && GUID.TryParse(avatar.thumbnailImageGuid, out var thumbnailImageGuid))
                        {
                            // 既存のアバターサムネイル画像に対する処理
                            if (withRegenerateThumbnails)
                            {
                                // 古いサムネイル画像を削除
                                AvatarThumbnailUtil.DeleteAvatarThumbnailImage(thumbnailImageGuid);
                            }
                            else
                            {
                                // サムネイル画像のファイル名を更新する
                                AvatarThumbnailUtil.RenameToGUID(thumbnailImageGuid);
                            }
                        }

                        if (!avatarThumbnailImageExists || withRegenerateThumbnails)
                        {
                            // サムネイル画像を新規作成または更新する
                            var thumbnail = AvatarThumbnailUtil.RenderAvatarThumbnail(avatarRenderer, avatarObject);
                            try
                            {
                                if (!string.IsNullOrEmpty(avatar.thumbnailImageGuid))
                                {
                                    AvatarThumbnailUtil.DeleteAvatarThumbnailImage(avatar.thumbnailImageGuid);
                                    avatar.thumbnailImageGuid = "";
                                }

                                avatar.thumbnailImageGuid = AvatarThumbnailUtil.StoreAvatarThumbnailImage(thumbnail).ToString();
                            }
                            finally
                            {
                                UnityEngine.Object.DestroyImmediate(thumbnail);
                            }
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
            RefreshIndexes(assetProductDetails);

            AssetDatabase.Refresh();
        }

        private bool IsAssetExists(string guid)
        {
            if (string.IsNullOrEmpty(guid))
            {
                return false;
            }

            return !string.IsNullOrEmpty(AssetDatabase.GUIDToAssetPath(guid));
        }

        private void RefreshIndexes(Dictionary<string, List<AssetProductDetail>> autoMatchedAssetProductDetails)
        {
            var avatarCatalogDatabase = AvatarCatalogDatabase.LoadOrCreateFile();
            var avatarSearchIndex = AvatarSearchIndex.LoadOrCreateFile();

            avatarSearchIndex.entries = avatarCatalogDatabase.avatars.Select(avatar =>
            {
                var autoMatchedAssetProductDetail = autoMatchedAssetProductDetails.GetValueOrDefault(avatar.avatarGlobalObjectId, new List<AssetProductDetail>());
                var avatarSearchIndexEntry = new AvatarSearchIndex.AvatarSearchIndexEntry();
                avatarSearchIndexEntry.avatarGlobalObjectId = avatar.avatarGlobalObjectId;
                avatarSearchIndexEntry.Values = GenerateAvatarSearchIndexWords(avatar, autoMatchedAssetProductDetail);
                return avatarSearchIndexEntry;
            }).ToList();

            AvatarSearchIndex.Save(avatarSearchIndex);
        }

        public void RefreshIndexes(GlobalObjectId avatarGlobalObjectId)
        {
            RefreshIndexes(avatarGlobalObjectId.ToString());
        }

        public void RefreshIndexes(string avatarGlobalObjectId)
        {
            var avatarCatalogDatabase = AvatarCatalogDatabase.LoadOrCreateFile();
            var avatarSearchIndex = AvatarSearchIndex.LoadOrCreateFile();

            var avatarCatalogDatabaseEntity = avatarCatalogDatabase.Get(avatarGlobalObjectId);
            if (avatarCatalogDatabaseEntity == null)
            {
                return;
            }

            List<AssetProductDetail> assetProductDetails = new List<AssetProductDetail>();
            SceneProcessor.ProcessSceneTemporarily(avatarCatalogDatabaseEntity.sceneAsset, (scene) =>
            {
                var parseResult = GlobalObjectId.TryParse(avatarCatalogDatabaseEntity.avatarGlobalObjectId, out var globalObjectId);
                if (!parseResult)
                {
                    return;
                }

                var targetAvatarObject = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(globalObjectId) as GameObject;
                if (targetAvatarObject == null)
                {
                    return;
                }

                var apd = GetReferencedAssetProductDetails(targetAvatarObject);
                assetProductDetails.AddRange(apd);
            });

            var avatarSearchIndexEntry = new AvatarSearchIndex.AvatarSearchIndexEntry();
            avatarSearchIndexEntry.avatarGlobalObjectId = avatarCatalogDatabaseEntity.avatarGlobalObjectId;
            avatarSearchIndexEntry.Values = GenerateAvatarSearchIndexWords(avatarCatalogDatabaseEntity, assetProductDetails);
            avatarSearchIndex.Set(avatarSearchIndexEntry);

            AvatarSearchIndex.Save(avatarSearchIndex);
        }

        private List<string> GenerateAvatarSearchIndexWords(AvatarCatalogDatabase.AvatarCatalogEntry avatar, IEnumerable<AssetProductDetail> assetProductDetails)
        {
            var words = new List<string> { avatar.avatarObjectName };
            var mergedAssetProductDetails = assetProductDetails.AsEnumerable();

            if (!string.IsNullOrEmpty(avatar.avatarMetadataGuid) && GUID.TryParse(avatar.avatarMetadataGuid, out var avatarMetadataGuid))
            {
                var avatarMetadata = AvatarMetadataUtil.LoadMetadata(avatarMetadataGuid);
                if (avatarMetadata != null)
                {
                    words.Add(avatarMetadata.comment);
                    words.AddRange(avatarMetadata.tags);

                    mergedAssetProductDetails = avatarMetadata.assetProductDetails
                        .Where(detail => detail != null)
                        .Concat(assetProductDetails);
                }
            }

            foreach (var assetProductDetail in mergedAssetProductDetails.Distinct())
            {
                words.Add(assetProductDetail.productName);
                words.Add(assetProductDetail.creatorName);
                var tags = assetProductDetail.tags.Where(tag => !string.IsNullOrEmpty(tag)).ToList();
                if (tags.Count > 0)
                {
                    words.AddRange(tags);
                }
            }

            return words.Distinct().ToList();
        }

        private void CleanupFiles(Dictionary<string, AvatarCatalogDatabase.AvatarCatalogEntry> prevAvatars, List<AvatarCatalogDatabase.AvatarCatalogEntry> newAvatars)
        {
            var newAvatarGlobalObjectIds = newAvatars.Select(avatar => avatar.avatarGlobalObjectId);
            var removedAvatars = prevAvatars.Values.Where(prevAvatar => !newAvatarGlobalObjectIds.Contains(prevAvatar.avatarGlobalObjectId));
            foreach (var removedAvatar in removedAvatars)
            {
                // サムネイル画像の削除
                if (GUID.TryParse(removedAvatar.thumbnailImageGuid, out var guid))
                {
                    var thumbnailImagePath = AssetDatabase.GUIDToAssetPath(guid);
                    if (!string.IsNullOrEmpty(thumbnailImagePath))
                    {
                        AssetDatabase.DeleteAsset(thumbnailImagePath);
                    }
                }
            }
        }

        public IEnumerable<AssetProductDetail> GetReferencedAssetProductDetails(GameObject go)
        {
            return GetReferencedAssetProductDetails(go, GetAllAssetProductDetails());
        }

        public IEnumerable<AssetProductDetail> GetReferencedAssetProductDetails(GameObject go, IEnumerable<(AssetProductDetail assetProductDetail, string folderPath)> assetProductDetailWithFolderPaths)
        {
            var dependencyPaths = GetDependencyPaths(go);

            return dependencyPaths
                .SelectMany(dependencyPath => FindAssetProductDetails(dependencyPath, assetProductDetailWithFolderPaths))
                .Distinct();
        }

        public IEnumerable<AssetProductDetail> GetReferencedAssetProductDetails(GameObject go, IEnumerable<AssetProductDetail> allAssetProductDetails)
        {
            var dependencyPaths = GetDependencyPaths(go);

            var detailWithPaths = allAssetProductDetails
            .Select(apd =>
            {
                var folderPath = string.IsNullOrEmpty(apd.rootFolderPath)
                    ? Path.GetDirectoryName(AssetDatabase.GetAssetPath(apd))
                    : apd.rootFolderPath;
                return (apd, folderPath.Replace("\\", "/"));
            })
            .ToList();

            return dependencyPaths
                .SelectMany(dependencyPath => FindAssetProductDetails(dependencyPath, detailWithPaths))
                .Distinct();
        }

        private IEnumerable<string> GetDependencyPaths(GameObject go)
        {
            var dependencies = GetDependencies(go);

            return dependencies.Select(dependency =>
            {
                var path = AssetDatabase.GetAssetPath(dependency);
                if (string.IsNullOrEmpty(path))
                {
                    return null;
                }

                return path;
            })
            .Where(path => !string.IsNullOrEmpty(path))
            .Distinct();
        }

        private UnityEngine.Object[] GetDependencies(GameObject go)
        {
            var roots = go.GetComponentsInChildren<Transform>(true)
                .Select(child => (UnityEngine.Object)child.gameObject)
                .ToArray();

            return EditorUtility.CollectDependencies(roots);
        }

        private IEnumerable<AssetProductDetail> FindAssetProductDetails(string assetFilePath, IEnumerable<(AssetProductDetail assetProductDetail, string folderPath)> assetProductDetailWithFolderPaths)
        {
            // 該当するパスの中で一番深い（長い）フォルダパスを見つける
            var longestMatchFolder = assetProductDetailWithFolderPaths
                .Select(x => x.folderPath)
                .Distinct()
                .Where(folderPath => IsSubPathOf(assetFilePath, folderPath))
                .OrderByDescending(folderPath => folderPath.Length)
                .FirstOrDefault();

            if (longestMatchFolder == null)
            {
                return Enumerable.Empty<AssetProductDetail>();
            }

            return assetProductDetailWithFolderPaths
                .Where(x => x.folderPath == longestMatchFolder)
                .Select(x => x.assetProductDetail);
        }

        private bool IsSubPathOf(string targetFilePath, string directoryPath)
        {
            if (string.IsNullOrEmpty(targetFilePath) || string.IsNullOrEmpty(directoryPath))
            {
                return false;
            }

            if (!directoryPath.EndsWith("/"))
            {
                directoryPath += "/";
            }

            StringComparison sc;
            if (Application.platform == RuntimePlatform.WindowsEditor || Application.platform == RuntimePlatform.WindowsPlayer || Application.platform == RuntimePlatform.WindowsServer)
            {
                sc = StringComparison.OrdinalIgnoreCase;
            }
            else
            {
                sc = StringComparison.Ordinal;
            }

            return targetFilePath.StartsWith(directoryPath, sc);
        }

        private IEnumerable<AssetProductDetail> GetAllAssetProductDetails()
        {
            return AssetDatabase.FindAssets($"t:{typeof(AssetProductDetail)}")
                .Select(assetGuid => AssetDatabase.GUIDToAssetPath(assetGuid))
                .Where(assetPath => !string.IsNullOrEmpty(assetPath))
                .Select(assetPath => AssetDatabase.LoadAssetAtPath<AssetProductDetail>(assetPath))
                .Where(asset => asset != null);
        }
    }
}

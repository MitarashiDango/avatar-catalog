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
        private MigrateAvatarMetadata migrateAvatarMetadata = new MigrateAvatarMetadata();

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

            var avatarCatalogDatabase = AvatarDatabase.LoadOrCreateFile();

            var previousAvatarDatabaseEntries = avatarCatalogDatabase.GetMappedAvatarCatalogEntries();

            var avatarDatabaseSources = new List<AvatarDatabaseSource>();

            using var avatarRenderer = new AvatarRenderer();

            SceneProcessor.WalkAllScenes((sceneAsset, currentScene) =>
            {
                var currentSceneRootObjects = currentScene.GetRootGameObjects();

                var extractedAvatars = currentSceneRootObjects
                    .Where(obj => obj != null && obj.GetComponent<VRCAvatarDescriptor>() != null)
                    .Select(avatarObject =>
                    {
                        // マイグレーション処理
                        AvatarMetadataUtil.MigrateAvatarMetadataSettings(avatarObject);
                        migrateAvatarMetadata.Do(avatarObject);

                        return (AvatarRootObject: avatarObject, ExtractedAvatarData: ExtractAvatarData(avatarObject));
                    });

                foreach (var extractedAvatar in extractedAvatars)
                {
                    if (!previousAvatarDatabaseEntries.ContainsKey(extractedAvatar.ExtractedAvatarData.avatarGlobalObjectId))
                    {
                        // 未追加のアバター
                        var thumbnail = AvatarThumbnailUtil.RenderAvatarThumbnail(avatarRenderer, extractedAvatar.AvatarRootObject);
                        try
                        {
                            avatarDatabaseSources.Add(new AvatarDatabaseSource()
                            {
                                avatarGlobalObjectId = extractedAvatar.ExtractedAvatarData.avatarGlobalObjectId,
                                avatarObjectName = extractedAvatar.ExtractedAvatarData.avatarObjectName,
                                sceneAssetGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(sceneAsset)),
                                thumbnailImageGuid = AvatarThumbnailUtil.StoreAvatarThumbnailImage(thumbnail).ToString(),
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
                        var previousAvatarEntry = previousAvatarDatabaseEntries[extractedAvatar.ExtractedAvatarData.avatarGlobalObjectId];

                        var avatarDatabaseSource = new AvatarDatabaseSource()
                        {
                            avatarGlobalObjectId = previousAvatarEntry.avatarGlobalObjectId,
                            avatarObjectName = extractedAvatar.ExtractedAvatarData.avatarObjectName,
                            sceneAssetGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(sceneAsset)),
                            thumbnailImageGuid = previousAvatarEntry.thumbnailImageGuid,
                        };

                        var avatarThumbnailImageExists = IsAssetFileExists(avatarDatabaseSource.thumbnailImageGuid);

                        if (avatarThumbnailImageExists && GUID.TryParse(avatarDatabaseSource.thumbnailImageGuid, out var thumbnailImageGuid))
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
                            var thumbnail = AvatarThumbnailUtil.RenderAvatarThumbnail(avatarRenderer, extractedAvatar.AvatarRootObject);
                            try
                            {
                                if (!string.IsNullOrEmpty(avatarDatabaseSource.thumbnailImageGuid))
                                {
                                    AvatarThumbnailUtil.DeleteAvatarThumbnailImage(avatarDatabaseSource.thumbnailImageGuid);
                                    avatarDatabaseSource.thumbnailImageGuid = "";
                                }

                                avatarDatabaseSource.thumbnailImageGuid = AvatarThumbnailUtil.StoreAvatarThumbnailImage(thumbnail).ToString();
                            }
                            finally
                            {
                                UnityEngine.Object.DestroyImmediate(thumbnail);
                            }
                        }

                        avatarDatabaseSources.Add(avatarDatabaseSource);
                    }
                }
            });

            // 不要となったファイルの削除
            CleanupFiles(previousAvatarDatabaseEntries, avatarDatabaseSources);

            avatarCatalogDatabase.avatars = avatarDatabaseSources
                .Select(source => source.GetAvatarDatabaseEntry())
                .ToList();

            AvatarDatabase.Save(avatarCatalogDatabase);

            // 検索インデックスの最新化
            RefreshIndexes(avatarDatabaseSources.Select(source => source.GetAvatarSearchIndexSource()));

            AssetDatabase.Refresh();
        }

        private bool IsAssetFileExists(string guid)
        {
            if (string.IsNullOrEmpty(guid))
            {
                return false;
            }

            return !string.IsNullOrEmpty(AssetDatabase.GUIDToAssetPath(guid));
        }

        private void RefreshIndexes(IEnumerable<AvatarSearchIndexSource> searchIndexSources)
        {
            var avatarSearchIndex = AvatarSearchIndex.LoadOrCreateFile();
            var allAssetProductDetails = GetAllAssetProductDetails();

            searchIndexSources.Select(searchIndexSource =>
            {
                // アバターオブジェクトが参照しているアセットの製品情報を自動検出する
                var autoMatchedAssetProductDetails = GetReferencedAssetProductDetails(searchIndexSource.dependencyPaths, allAssetProductDetails);

                return new AvatarSearchIndex.AvatarSearchIndexEntry
                {
                    avatarGlobalObjectId = searchIndexSource.avatarGlobalObjectId,
                    Values = GenerateAvatarSearchIndexWords(
                      searchIndexSource, autoMatchedAssetProductDetails)
                };
            }).ToList();

            AvatarSearchIndex.Save(avatarSearchIndex);
        }

        private List<string> GenerateAvatarSearchIndexWords(AvatarSearchIndexSource searchIndexSource, IEnumerable<ExtractedAssetProductDetail> autoMatchedAssetProductDetails)
        {
            var words = new List<string> { searchIndexSource.avatarObjectName };
            var mergedAssetProductDetails = autoMatchedAssetProductDetails.AsEnumerable();

            // アバターメタデータが付与されている場合、検索対象とする
            var avatarMetadata = searchIndexSource.avatarMetadata;
            if (avatarMetadata != null)
            {
                words.Add(avatarMetadata.comment);
                words.AddRange(avatarMetadata.tags);

                mergedAssetProductDetails = avatarMetadata.assetProductDetails
                    .Where(detail => detail != null)
                    .Concat(autoMatchedAssetProductDetails);
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

        private void CleanupFiles(Dictionary<string, AvatarDatabase.AvatarDatabaseEntry> previousAvatarEntries, List<AvatarDatabaseSource> avatarDatabaseSources)
        {
            var newAvatarGlobalObjectIds = avatarDatabaseSources.Select(avatar => avatar.avatarGlobalObjectId);
            var removedAvatars = previousAvatarEntries.Values.Where(prevAvatar => !newAvatarGlobalObjectIds.Contains(prevAvatar.avatarGlobalObjectId));
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

        private ExtractedAvatarData ExtractAvatarData(GameObject avatarRootObject)
        {
            var avatarGlobalObjectId = GlobalObjectId.GetGlobalObjectIdSlow(avatarRootObject);

            var ead = new ExtractedAvatarData()
            {
                avatarGlobalObjectId = avatarGlobalObjectId.ToString(),
                avatarObjectName = avatarRootObject.name,
                dependencyPaths = GetDependencyPaths(avatarRootObject).ToList(),
            };

            var avatarMetadata = avatarRootObject.GetComponent<AvatarMetadata>();
            if (avatarMetadata != null)
            {
                ead.avatarMetadata = new ExtractedAvatarMetadata(avatarMetadata);
            }

            return ead;
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

        private IEnumerable<ExtractedAssetProductDetail> GetReferencedAssetProductDetails(List<string> dependencyPaths, IEnumerable<ExtractedAssetProductDetail> allAssetProductDetails)
        {
            return dependencyPaths
                .SelectMany(dependencyPath => FindAssetProductDetails(dependencyPath, allAssetProductDetails))
                .Distinct();
        }

        private IEnumerable<ExtractedAssetProductDetail> FindAssetProductDetails(string assetFilePath, IEnumerable<ExtractedAssetProductDetail> allAssetProductDetails)
        {
            // 該当するパスの中で一番深い（長い）フォルダパスを見つける
            var longestMatchFolder = allAssetProductDetails
                .Select(assetProductDetail => assetProductDetail.rootFolderPath)
                .Distinct()
                .Where(folderPath => IsSubPathOf(assetFilePath, folderPath))
                .OrderByDescending(folderPath => folderPath.Length)
                .FirstOrDefault();

            if (longestMatchFolder == null)
            {
                return Enumerable.Empty<ExtractedAssetProductDetail>();
            }

            return allAssetProductDetails
                .Where(assetProductDetail => assetProductDetail.rootFolderPath == longestMatchFolder);
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

        private List<ExtractedAssetProductDetail> GetAllAssetProductDetails()
        {
            return AssetDatabase.FindAssets($"t:{typeof(AssetProductDetail)}")
                .Select(assetGuid => AssetDatabase.GUIDToAssetPath(assetGuid))
                .Where(assetPath => !string.IsNullOrEmpty(assetPath))
                .Select(assetPath => AssetDatabase.LoadAssetAtPath<AssetProductDetail>(assetPath))
                .Where(asset => asset != null)
                .Select(assetProductDetail => new ExtractedAssetProductDetail(assetProductDetail))
                .ToList();
        }

        internal class ExtractedAvatarData
        {
            public string avatarGlobalObjectId = "";
            public string avatarObjectName = "";
            public List<string> dependencyPaths = new List<string>();
            public ExtractedAvatarMetadata avatarMetadata;
        }

        internal class AvatarDatabaseSource
        {
            public string avatarGlobalObjectId = "";
            public string avatarObjectName = "";
            public List<string> dependencyPaths = new List<string>();
            public ExtractedAvatarMetadata avatarMetadata;
            public string sceneAssetGuid;
            public string thumbnailImageGuid = "";

            public AvatarSearchIndexSource GetAvatarSearchIndexSource()
            {
                return new AvatarSearchIndexSource
                {
                    avatarGlobalObjectId = avatarGlobalObjectId,
                    avatarObjectName = avatarObjectName,
                    dependencyPaths = dependencyPaths,
                    avatarMetadata = avatarMetadata
                };
            }

            public AvatarDatabase.AvatarDatabaseEntry GetAvatarDatabaseEntry()
            {
                return new AvatarDatabase.AvatarDatabaseEntry
                {
                    avatarGlobalObjectId = avatarGlobalObjectId,
                    avatarObjectName = avatarObjectName,
                    sceneAssetGuid = sceneAssetGuid,
                    thumbnailImageGuid = thumbnailImageGuid,
                };
            }
        }

        internal class AvatarSearchIndexSource
        {
            public string avatarGlobalObjectId = "";
            public string avatarObjectName = "";
            public List<string> dependencyPaths = new List<string>();
            public ExtractedAvatarMetadata avatarMetadata;
        }

        internal class ExtractedAvatarMetadata
        {
            public ExtractedAvatarMetadata(AvatarMetadata avatarMetadata)
            {
                comment = avatarMetadata.comment;
                tags = avatarMetadata.tags.ToList();
                assetProductDetails = avatarMetadata.assetProductDetails.Select(apd => new ExtractedAssetProductDetail(apd)).ToList();
            }

            public string comment = "";
            public List<string> tags = new List<string>();
            public List<ExtractedAssetProductDetail> assetProductDetails = new List<ExtractedAssetProductDetail>();
        }

        internal class ExtractedAssetProductDetail
        {
            public ExtractedAssetProductDetail(AssetProductDetail apd)
            {
                fileGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(apd));
                var folderPath = string.IsNullOrEmpty(apd.rootFolderPath)
                    ? Path.GetDirectoryName(AssetDatabase.GetAssetPath(apd))
                    : apd.rootFolderPath;
                rootFolderPath = folderPath.Replace("\\", "/");

                productName = apd.productName;
                creatorName = apd.creatorName;
                productUrl = apd.productUrl;
                releaseDateTime = apd.releaseDateTime;
                tags = apd.tags.ToList();
                description = apd.description;
                licenses = apd.licenses.ToList();
            }

            public string fileGuid = "";
            public string rootFolderPath = "";
            public string productName;
            public string creatorName;
            public string productUrl;
            public string releaseDateTime;
            public List<string> tags;
            public string description;
            public List<License> licenses;

            public bool Equals(ExtractedAssetProductDetail eapd)
            {
                return !string.IsNullOrEmpty(fileGuid) && !string.IsNullOrEmpty(eapd.fileGuid) && fileGuid == eapd.fileGuid;
            }
        }
    }
}

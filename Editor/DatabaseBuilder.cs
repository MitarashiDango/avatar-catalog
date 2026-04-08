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

            var sceneEntries = new List<AvatarDatabase.SceneEntry>();
            var avatarDatabaseSources = new List<AvatarDatabaseSource>();

            using var avatarRenderer = new AvatarRenderer();

            var allSceneAssetPaths = SceneProcessor.GetAllSceneAssetPaths();

            SceneProcessor.WalkScenes(allSceneAssetPaths, (sceneAsset, currentScene) =>
            {
                sceneEntries.Add(new AvatarDatabase.SceneEntry()
                {
                    sceneName = sceneAsset.name,
                    sceneAssetGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(sceneAsset)),
                });

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
                                avatarMetadata = extractedAvatar.ExtractedAvatarData.avatarMetadata,
                                dependencyPaths = extractedAvatar.ExtractedAvatarData.dependencyPaths,
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
                            avatarMetadata = extractedAvatar.ExtractedAvatarData.avatarMetadata,
                            dependencyPaths = extractedAvatar.ExtractedAvatarData.dependencyPaths,
                        };

                        var avatarThumbnailImageExists = IsAssetFileExists(avatarDatabaseSource.thumbnailImageGuid);

                        if (avatarThumbnailImageExists && GUID.TryParse(avatarDatabaseSource.thumbnailImageGuid, out var thumbnailImageGuid))
                        {
                            // 既存のアバターサムネイル画像に対する処理
                            if (withRegenerateThumbnails)
                            {
                                // 古いサムネイル画像を削除
                                AvatarThumbnailUtil.DeleteAvatarThumbnailImage(thumbnailImageGuid);
                                avatarDatabaseSource.thumbnailImageGuid = "";
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

            avatarCatalogDatabase.orderedScenes = sceneEntries;
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
            var folderToProductDetails = BuildFolderToProductDetailsMap(allAssetProductDetails);

            avatarSearchIndex.entries = searchIndexSources.Select(searchIndexSource =>
            {
                // アバターオブジェクトが参照しているアセットの製品情報を自動検出する
                var autoMatchedAssetProductDetails = GetReferencedAssetProductDetails(searchIndexSource.dependencyPaths, folderToProductDetails);

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
            var newAvatarGlobalObjectIds = new HashSet<string>(avatarDatabaseSources.Select(avatar => avatar.avatarGlobalObjectId));
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

        private static StringComparer GetPathStringComparer()
        {
            if (Application.platform == RuntimePlatform.WindowsEditor || Application.platform == RuntimePlatform.WindowsPlayer || Application.platform == RuntimePlatform.WindowsServer)
            {
                return StringComparer.OrdinalIgnoreCase;
            }

            return StringComparer.Ordinal;
        }

        /// <summary>
        /// フォルダパスをキーとした製品情報のルックアップテーブルを構築する
        /// </summary>
        private Dictionary<string, List<ExtractedAssetProductDetail>> BuildFolderToProductDetailsMap(List<ExtractedAssetProductDetail> allAssetProductDetails)
        {
            var comparer = GetPathStringComparer();
            var map = new Dictionary<string, List<ExtractedAssetProductDetail>>(comparer);

            foreach (var detail in allAssetProductDetails)
            {
                var key = detail.rootFolderPath.TrimEnd('/');
                if (!map.TryGetValue(key, out var list))
                {
                    list = new List<ExtractedAssetProductDetail>();
                    map[key] = list;
                }
                list.Add(detail);
            }

            return map;
        }

        private IEnumerable<ExtractedAssetProductDetail> GetReferencedAssetProductDetails(List<string> dependencyPaths, Dictionary<string, List<ExtractedAssetProductDetail>> folderToProductDetails)
        {
            return dependencyPaths
                .SelectMany(dependencyPath => FindAssetProductDetails(dependencyPath, folderToProductDetails))
                .Distinct();
        }

        /// <summary>
        /// 依存パスのディレクトリを親方向に辿り、最長一致するフォルダパスの製品情報を返す
        /// </summary>
        private IEnumerable<ExtractedAssetProductDetail> FindAssetProductDetails(string assetFilePath, Dictionary<string, List<ExtractedAssetProductDetail>> folderToProductDetails)
        {
            var dir = Path.GetDirectoryName(assetFilePath)?.Replace("\\", "/");

            while (!string.IsNullOrEmpty(dir))
            {
                if (folderToProductDetails.TryGetValue(dir, out var products))
                {
                    return products;
                }

                var parent = Path.GetDirectoryName(dir)?.Replace("\\", "/");
                if (parent == dir)
                {
                    break;
                }
                dir = parent;
            }

            return Enumerable.Empty<ExtractedAssetProductDetail>();
        }

        private List<ExtractedAssetProductDetail> GetAllAssetProductDetails()
        {
            return AssetDatabase.FindAssets($"t:{typeof(AssetProductDetail)}")
                .Select(assetGuid => AssetDatabase.GUIDToAssetPath(assetGuid))
                .Where(assetPath => !string.IsNullOrEmpty(assetPath))
                .Select(assetPath => AssetDatabase.LoadAssetAtPath<AssetProductDetail>(assetPath))
                .Where(asset => asset != null)
                .SelectMany(assetProductDetail => ExtractedAssetProductDetail.FromAssetProductDetail(assetProductDetail))
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
                assetProductDetails = avatarMetadata.assetProductDetails
                    .SelectMany(apd => ExtractedAssetProductDetail.FromAssetProductDetail(apd))
                    .ToList();
            }

            public string comment = "";
            public List<string> tags = new List<string>();
            public List<ExtractedAssetProductDetail> assetProductDetails = new List<ExtractedAssetProductDetail>();
        }

        internal class ExtractedAssetProductDetail
        {
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

            public override bool Equals(object obj) => Equals(obj as ExtractedAssetProductDetail);

            public override int GetHashCode() => fileGuid?.GetHashCode() ?? 0;

            public static IEnumerable<ExtractedAssetProductDetail> FromAssetProductDetail(AssetProductDetail assetProductDetail)
            {
                var assetFilePath = AssetDatabase.GetAssetPath(assetProductDetail);
                var fileGuid = AssetDatabase.AssetPathToGUID(assetFilePath);

                var rootFolderPaths = new List<string>(assetProductDetail.rootFolderPaths);
#pragma warning disable CS0612
                if (!string.IsNullOrEmpty(assetProductDetail.rootFolderPath))
                {
                    rootFolderPaths.Add(assetProductDetail.rootFolderPath);
                }
#pragma warning restore CS0612

                return rootFolderPaths
                    .Where(path => !string.IsNullOrEmpty(path))
                    .DefaultIfEmpty(Path.GetDirectoryName(assetFilePath))
                    .Select(path => path.Replace("\\", "/"))
                    .Select(rootFolderPath =>
                    {
                        return new ExtractedAssetProductDetail()
                        {
                            fileGuid = fileGuid,
                            rootFolderPath = rootFolderPath,
                            productName = assetProductDetail.productName,
                            creatorName = assetProductDetail.creatorName,
                            productUrl = assetProductDetail.productUrl,
                            releaseDateTime = assetProductDetail.releaseDateTime,
                            tags = assetProductDetail.tags.ToList(),
                            description = assetProductDetail.description,
                            licenses = assetProductDetail.licenses.ToList(),
                        };
                    });
            }
        }
    }
}

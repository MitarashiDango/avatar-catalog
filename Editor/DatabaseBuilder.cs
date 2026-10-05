using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MitarashiDango.AvatarCatalog.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
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

        private static string ProgressBarTitle => AcL10n.Tr("progress.build_avatar_database.title");

        /// <summary>
        /// アバターカタログデータベースと各種インデックスを構築する
        /// </summary>
        /// <param name="withRegenerateThumbnails">更新時にサムネイル画像も新しくするか</param>
        /// <returns>完了した場合は true、キャンセルされた場合は false を返す</returns>
        public bool BuildAvatarCatalogDatabaseAndIndexes(bool withRegenerateThumbnails = false)
        {
            CreateFolders();

            var avatarCatalogDatabase = AvatarDatabase.LoadOrCreateFile();

            var previousAvatarDatabaseEntries = avatarCatalogDatabase.GetMappedAvatarCatalogEntries();

            var sceneEntries = new List<AvatarDatabase.SceneEntry>();
            var avatarDatabaseSources = new List<AvatarDatabaseSource>();

            var allSceneAssetPaths = SceneProcessor.GetAllSceneAssetPaths().ToList();

            try
            {
                if (!ProcessScenes(allSceneAssetPaths, previousAvatarDatabaseEntries, sceneEntries, avatarDatabaseSources, withRegenerateThumbnails))
                {
                    return false;
                }

                // 不要となったファイルの削除
                EditorUtility.DisplayProgressBar(ProgressBarTitle, AcL10n.Tr("progress.build_avatar_database.cleanup"), 0.93f);
                CleanupFiles(previousAvatarDatabaseEntries, avatarDatabaseSources);

                avatarCatalogDatabase.orderedScenes = sceneEntries;
                avatarCatalogDatabase.avatars = avatarDatabaseSources
                    .Select(source => source.GetAvatarDatabaseEntry())
                    .ToList();

                EditorUtility.DisplayProgressBar(ProgressBarTitle, AcL10n.Tr("progress.build_avatar_database.saving"), 0.96f);
                AvatarDatabase.Save(avatarCatalogDatabase);

                // 検索インデックスの最新化
                EditorUtility.DisplayProgressBar(ProgressBarTitle, AcL10n.Tr("progress.build_avatar_database.refreshing_index"), 0.98f);
                RefreshIndexes(avatarDatabaseSources.Select(source => source.GetAvatarSearchIndexSource()));

                AssetDatabase.Refresh();

                return true;
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        /// <summary>
        /// Assets フォルダー内の指定したシーンのアバターと検索インデックスを更新する
        /// </summary>
        /// <param name="sceneAssetPaths">更新対象のシーンアセットパス。ロードされていないシーンは一時的に開く</param>
        /// <returns>完了した場合は true、キャンセルまたは更新できない場合は false を返す</returns>
        public bool UpdateScenes(IEnumerable<string> sceneAssetPaths)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog(
                    AcL10n.Tr("dialog.title.info"),
                    AcL10n.Tr("info.play_mode_cannot_update_database"),
                    AcL10n.Tr("dialog.button.ok"));
                return false;
            }

            var allSceneAssetPaths = SceneProcessor.GetAllSceneAssetPaths().ToList();
            var allSceneAssetPathSet = allSceneAssetPaths.ToHashSet();
            var targetSceneAssetPaths = sceneAssetPaths.Where(allSceneAssetPathSet.Contains).Distinct().ToList();
            if (targetSceneAssetPaths.Count == 0)
            {
                EditorUtility.DisplayDialog(AcL10n.Tr("dialog.title.info"), AcL10n.Tr("info.no_scenes_to_update"), AcL10n.Tr("dialog.button.ok"));
                return false;
            }

            var avatarCatalogDatabase = AvatarDatabase.Load();
            var avatarSearchIndex = AvatarSearchIndex.Load();
            if (avatarCatalogDatabase == null || avatarSearchIndex == null)
            {
                EditorUtility.DisplayDialog(AcL10n.Tr("dialog.title.info"), AcL10n.Tr("info.avatar_database_rebuild_required"), AcL10n.Tr("dialog.button.ok"));
                return false;
            }

            var loadedTargetScenes = targetSceneAssetPaths
                .Select(path => EditorSceneManager.GetSceneByPath(path))
                .Where(scene => scene.isLoaded)
                .ToArray();
            if (loadedTargetScenes.Any(scene => scene.isDirty))
            {
                if (!EditorSceneManager.SaveModifiedScenesIfUserWantsTo(loadedTargetScenes))
                {
                    return false;
                }

                // 「保存しない」でも true が返るため、未保存の変更が残っていれば更新しない
                if (loadedTargetScenes.Any(scene => scene.isDirty))
                {
                    EditorUtility.DisplayDialog(AcL10n.Tr("dialog.title.info"), AcL10n.Tr("info.save_scenes_before_update"), AcL10n.Tr("dialog.button.ok"));
                    return false;
                }
            }

            CreateFolders();

            var targetSceneGuids = targetSceneAssetPaths.Select(path => AssetDatabase.AssetPathToGUID(path)).ToHashSet();
            var previousAvatarEntries = avatarCatalogDatabase.avatars
                .Where(avatar => targetSceneGuids.Contains(avatar.sceneAssetGuid))
                .ToDictionary(avatar => avatar.avatarGlobalObjectId);
            var sceneEntries = new List<AvatarDatabase.SceneEntry>();
            var avatarDatabaseSources = new List<AvatarDatabaseSource>();

            try
            {
                if (!ProcessScenes(targetSceneAssetPaths, previousAvatarEntries, sceneEntries, avatarDatabaseSources, false))
                {
                    return false;
                }

                var (orderedScenes, avatars) = MergeSceneEntries(
                    avatarCatalogDatabase, targetSceneGuids, sceneEntries, avatarDatabaseSources, allSceneAssetPaths);

                // 検索インデックスの最新化
                EditorUtility.DisplayProgressBar(ProgressBarTitle, AcL10n.Tr("progress.build_avatar_database.refreshing_index"), 0.93f);
                var avatarOrder = avatars
                    .Select((avatar, index) => (avatar.avatarGlobalObjectId, index))
                    .ToDictionary(item => item.avatarGlobalObjectId, item => item.index);
                var updatedSearchIndexEntries = GenerateSearchIndexEntries(avatarDatabaseSources.Select(source => source.GetAvatarSearchIndexSource()));
                var updatedAvatarIds = updatedSearchIndexEntries.Select(entry => entry.avatarGlobalObjectId).ToHashSet();
                var searchIndexEntries = avatarSearchIndex.entries
                    .Where(entry => avatarOrder.ContainsKey(entry.avatarGlobalObjectId) && !updatedAvatarIds.Contains(entry.avatarGlobalObjectId))
                    .Concat(updatedSearchIndexEntries)
                    .OrderBy(entry => avatarOrder[entry.avatarGlobalObjectId])
                    .ToList();

                // 不要となったファイルの削除
                EditorUtility.DisplayProgressBar(ProgressBarTitle, AcL10n.Tr("progress.build_avatar_database.cleanup"), 0.96f);
                CleanupFiles(previousAvatarEntries, avatarDatabaseSources);

                EditorUtility.DisplayProgressBar(ProgressBarTitle, AcL10n.Tr("progress.build_avatar_database.saving"), 0.98f);
                avatarCatalogDatabase.orderedScenes = orderedScenes;
                avatarCatalogDatabase.avatars = avatars;
                AvatarDatabase.Save(avatarCatalogDatabase);

                avatarSearchIndex.entries = searchIndexEntries;
                AvatarSearchIndex.Save(avatarSearchIndex);

                AssetDatabase.Refresh();
                return true;
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        private (List<AvatarDatabase.SceneEntry> orderedScenes, List<AvatarDatabase.AvatarDatabaseEntry> avatars) MergeSceneEntries(
            AvatarDatabase avatarCatalogDatabase,
            HashSet<string> targetSceneGuids,
            List<AvatarDatabase.SceneEntry> sceneEntries,
            List<AvatarDatabaseSource> avatarDatabaseSources,
            List<string> allSceneAssetPaths)
        {
            // 対象の絞り込みに使った全シーンの一覧から、全件再構築と同じ順序を取得する
            var allSceneOrder = allSceneAssetPaths
                .Select((path, index) => (guid: AssetDatabase.AssetPathToGUID(path), index))
                .ToDictionary(item => item.guid, item => item.index);
            var orderedScenes = avatarCatalogDatabase.orderedScenes
                .Where(scene => !targetSceneGuids.Contains(scene.sceneAssetGuid))
                .Concat(sceneEntries)
                .OrderBy(scene => allSceneOrder.TryGetValue(scene.sceneAssetGuid, out var index) ? index : int.MaxValue)
                .ToList();

            var sceneOrder = orderedScenes
                .Select((scene, index) => (scene.sceneAssetGuid, index))
                .ToDictionary(item => item.sceneAssetGuid, item => item.index);
            // OrderBy は同じシーン内の順序を保つため、抽出時のルートオブジェクト順を維持できる
            var avatars = avatarCatalogDatabase.avatars
                .Where(avatar => !targetSceneGuids.Contains(avatar.sceneAssetGuid))
                .Concat(avatarDatabaseSources.Select(source => source.GetAvatarDatabaseEntry()))
                .OrderBy(avatar => sceneOrder.TryGetValue(avatar.sceneAssetGuid, out var index) ? index : int.MaxValue)
                .ToList();

            return (orderedScenes, avatars);
        }

        private void CreateFolders()
        {
            FolderUtil.CreateUserDataFolders();
            FolderUtil.CreateCacheFolder();
            FolderUtil.CreateAvatarThumbnailsCacheFolder();
        }

        private bool ProcessScenes(
            List<string> sceneAssetPaths,
            Dictionary<string, AvatarDatabase.AvatarDatabaseEntry> previousAvatarDatabaseEntries,
            List<AvatarDatabase.SceneEntry> sceneEntries,
            List<AvatarDatabaseSource> avatarDatabaseSources,
            bool withRegenerateThumbnails)
        {
            using var avatarRenderer = new AvatarRenderer();
            var totalScenes = sceneAssetPaths.Count;
            // シーン走査に進捗の 90% を割り当て、残りの 10% を集計処理で消費する
            const float SceneWalkPhaseRatio = 0.9f;

            for (var sceneIndex = 0; sceneIndex < totalScenes; sceneIndex++)
            {
                var sceneAssetPath = sceneAssetPaths[sceneIndex];
                var displaySceneName = Path.GetFileNameWithoutExtension(sceneAssetPath);
                var sceneProgress = (float)sceneIndex / totalScenes * SceneWalkPhaseRatio;

                if (EditorUtility.DisplayCancelableProgressBar(
                    ProgressBarTitle,
                    AcL10n.Tr("progress.build_avatar_database.processing_scene", sceneIndex + 1, totalScenes, displaySceneName),
                    sceneProgress))
                {
                    // シーン境界でキャンセルされたため、途中結果は保存せず終了する
                    return false;
                }

                var sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(sceneAssetPath);
                SceneProcessor.ProcessSceneTemporarily(sceneAssetPath, currentScene =>
                {
                    ProcessScene(
                        sceneAsset,
                        currentScene,
                        avatarRenderer,
                        previousAvatarDatabaseEntries,
                        sceneEntries,
                        avatarDatabaseSources,
                        withRegenerateThumbnails);
                });
            }

            return true;
        }

        private void ProcessScene(
            SceneAsset sceneAsset,
            UnityEngine.SceneManagement.Scene currentScene,
            AvatarRenderer avatarRenderer,
            Dictionary<string, AvatarDatabase.AvatarDatabaseEntry> previousAvatarDatabaseEntries,
            List<AvatarDatabase.SceneEntry> sceneEntries,
            List<AvatarDatabaseSource> avatarDatabaseSources,
            bool withRegenerateThumbnails)
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
            avatarSearchIndex.entries = GenerateSearchIndexEntries(searchIndexSources);
            AvatarSearchIndex.Save(avatarSearchIndex);
        }

        private List<AvatarSearchIndex.AvatarSearchIndexEntry> GenerateSearchIndexEntries(IEnumerable<AvatarSearchIndexSource> searchIndexSources)
        {
            var allAssetProductDetails = GetAllAssetProductDetails();
            var folderToProductDetails = BuildFolderToProductDetailsMap(allAssetProductDetails);

            return searchIndexSources.Select(searchIndexSource =>
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

using System.Collections.Generic;
using System.IO;
using MitarashiDango.AvatarCatalog.Runtime;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace MitarashiDango.AvatarCatalog
{
    public class AvatarMetadataUtil
    {
        /// <summary>
        /// アバターメタデータのアセットパスを取得します
        /// </summary>
        /// <param name="filename">対象のアセットファイル名</param>
        /// <returns>アバターメタデータのファイルパスを返却する。</returns>
        public static string GetMetadataPath(string filename)
        {
            return $"{FolderUtil.AvatarMetadataFolderPath}/{filename}.asset";
        }

        /// <summary>
        /// 指定されたアバターオブジェクトに対応するアバターメタデータをロードします
        /// </summary>
        /// <param name="avatarRootObject">対象のアバターオブジェクト</param>
        /// <returns>ロードされたアバターメタデータを返却する。GlobalObjectId が無効な場合は null を返却する。</returns>
        public static AvatarMetadata LoadMetadata(GameObject avatarRootObject)
        {
            if (avatarRootObject == null)
            {
                return null;
            }

            // マイグレーション処理
            MigrateAvatarMetadataSettings(avatarRootObject);

            var avatarMetadataSettings = avatarRootObject.GetComponent<AvatarMetadataSettings>();
            if (avatarMetadataSettings == null)
            {
                return null;
            }

            return avatarMetadataSettings.avatarMetadata;
        }

        /// <summary>
        /// アバターメタデータをロードします
        /// </summary>
        /// <param name="avatarMetadataGuid">アバターメタデータファイルのGUID</param>
        /// <returns>ロードされたアバターメタデータを返却する。ロード出来なかった場合は null を返却する。</returns>
        public static AvatarMetadata LoadMetadata(GUID avatarMetadataGuid)
        {
            if (avatarMetadataGuid.Empty())
            {
                return null;
            }

            var filePath = AssetDatabase.GUIDToAssetPath(avatarMetadataGuid);
            if (string.IsNullOrEmpty(filePath))
            {
                return null;
            }

            return AssetDatabase.LoadAssetAtPath<AvatarMetadata>(filePath);
        }

        /// <summary>
        /// アバターメタデータへの変更を保存します
        /// </summary>
        /// <param name="metadata">保存するアバターメタデータ</param>
        public static void SaveMetadata(AvatarMetadata metadata)
        {
            if (metadata != null)
            {
                EditorUtility.SetDirty(metadata);
                AssetDatabase.SaveAssets();
            }
            else
            {
                Debug.LogWarning("Attempted to save null metadata.");
            }
        }

        public static AvatarMetadata CreateMetadata(GameObject avatarRootObject)
        {
            if (avatarRootObject == null)
            {
                return null;
            }

            var vrcDescriptor = avatarRootObject.GetComponent<VRCAvatarDescriptor>();
            if (vrcDescriptor == null)
            {
                Debug.LogWarning($"GameObject '{avatarRootObject.name}' does not have a VRCAvatarDescriptor component. Cannot create metadata.");
                return null;
            }

            // マイグレーション処理
            MigrateAvatarMetadataSettings(avatarRootObject);

            string metadataPath;
            var avatarMetadataSettings = avatarRootObject.GetComponent<AvatarMetadataSettings>();
            if (avatarMetadataSettings != null && avatarMetadataSettings.avatarMetadata != null)
            {
                metadataPath = AssetDatabase.GetAssetPath(avatarMetadataSettings.avatarMetadata);
                Debug.LogWarning($"Metadata already exists for {avatarRootObject.name} at {metadataPath}. Returning existing one.");
                return avatarMetadataSettings.avatarMetadata;
            }

            AvatarMetadata newMetadata = ScriptableObject.CreateInstance<AvatarMetadata>();
            newMetadata.comment = "";
            newMetadata.tags = new List<string>();

            var avatarGlobalObjectId = GlobalObjectId.GetGlobalObjectIdSlow(avatarRootObject);
            newMetadata.avatarGlobalObjectId = avatarGlobalObjectId.ToString();

            metadataPath = AssetDatabase.GenerateUniqueAssetPath(GetMetadataPath($"tmp_{GUID.Generate()}"));

            try
            {
                FolderUtil.CreateUserDataFolder();
                FolderUtil.CreateAvatarMetadataFolder();

                AssetDatabase.CreateAsset(newMetadata, metadataPath);
                AssetDatabase.SaveAssets();

                var fileGuid = AssetDatabase.AssetPathToGUID(metadataPath);
                AssetDatabase.RenameAsset(metadataPath, fileGuid);
                metadataPath = AssetDatabase.GUIDToAssetPath(fileGuid);

                AssetDatabase.Refresh();

                if (avatarMetadataSettings == null)
                {
                    // アバターオブジェクトへコンポーネントを追加
                    avatarMetadataSettings = avatarRootObject.AddComponent<AvatarMetadataSettings>();
                }

                avatarMetadataSettings.avatarMetadata = newMetadata;

                EditorUtility.SetDirty(avatarMetadataSettings);
                AssetDatabase.SaveAssets();

                Debug.Log($"Created metadata for {avatarRootObject.name} at {metadataPath}");
                return newMetadata;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to create metadata asset at {metadataPath}: {e.Message}");

                // 作成失敗した場合、インスタンスがメモリに残る可能性があるので破棄
                if (newMetadata != null)
                {
                    Object.DestroyImmediate(newMetadata);
                }

                return null;
            }
        }

        public static bool DeleteMetadata(GameObject avatarRootObject)
        {
            if (avatarRootObject == null)
            {
                return false;
            }

            var metadataPath = "";
            var avatarMetadataSettings = avatarRootObject.GetComponent<AvatarMetadataSettings>();
            if (avatarMetadataSettings != null)
            {
                metadataPath = AssetDatabase.GetAssetPath(avatarMetadataSettings.avatarMetadata);
            }

            // アセットが存在するかどうかで判断
            if (AssetDatabase.LoadAssetAtPath<AvatarMetadata>(metadataPath) == null)
            {
                Debug.LogWarning($"Metadata not found for {avatarRootObject.name} at {metadataPath}. Cannot delete.");
                return false;
            }

            // アバターオブジェクトからコンポーネントを削除
            if (avatarMetadataSettings != null)
            {
                Object.DestroyImmediate(avatarMetadataSettings);
            }

            bool result = AssetDatabase.DeleteAsset(metadataPath);
            if (result)
            {
                Debug.Log($"Deleted metadata for {avatarRootObject.name} at {metadataPath}");

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
            else
            {
                Debug.LogError($"Failed to delete metadata for {avatarRootObject.name} at {metadataPath}");
            }

            return result;
        }

        public static bool MigrateAvatarMetadataSettings(GameObject avatarRootObject)
        {
            var avatarMetadataSettings = avatarRootObject.GetComponent<AvatarMetadataSettings>();
            if (avatarMetadataSettings != null)
            {
                if (avatarMetadataSettings.avatarMetadata == null)
                {
                    return false;
                }

                if (avatarMetadataSettings.avatarMetadata.avatarGlobalObjectId == "")
                {
                    var avatarGlobalObjectId = GlobalObjectId.GetGlobalObjectIdSlow(avatarRootObject);
                    avatarMetadataSettings.avatarMetadata.avatarGlobalObjectId = avatarGlobalObjectId.ToString();

                    EditorUtility.SetDirty(avatarMetadataSettings.avatarMetadata);
                }

                RenameToGuid(avatarMetadataSettings.avatarMetadata);
            }

            return true;
        }

        public static void RenameToGuid(AvatarMetadata avatarMetadata)
        {
            var filePath = AssetDatabase.GetAssetPath(avatarMetadata);
            if (string.IsNullOrEmpty(filePath))
            {
                return;
            }

            var fileGuid = AssetDatabase.AssetPathToGUID(filePath);

            // ファイル名が変更されていない場合、何も行わない
            if (Path.GetFileNameWithoutExtension(filePath) == fileGuid)
            {
                return;
            }

            AssetDatabase.RenameAsset(filePath, fileGuid);
        }
    }
}

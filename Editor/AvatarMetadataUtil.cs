using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace MitarashiDango.AvatarCatalog
{
    public class AvatarMetadataUtil
    {
        public const string AvatarMetadataFolderPath = "Assets/Avatar Catalog User Data/AvatarMetadata";

        /// <summary>
        /// GlobalObjectIdからアセットパスを取得します
        /// </summary>
        /// <param name="id">対象の GlobalObjectId</param>
        /// <returns>アバターメタデータのファイルパスを返却する。無効な GlobalObjectId の場合は null を返却する。</returns>
        public static string GetMetadataPath(GlobalObjectId id)
        {
            if (id.Equals(default) && id.identifierType == 0)
            {
                Debug.LogWarning("Attempted to get metadata path for an invalid GlobalObjectId.");
                return null;
            }

            FolderUtil.CreateUserDataFolder();
            FolderUtil.CreateAvatarMetadataFolder();

            return $"{AvatarMetadataFolderPath}/{id.assetGUID}_{id.targetObjectId}_{id.targetPrefabId}.asset";
        }

        /// <summary>
        /// 指定されたアバターオブジェクトに対応するアバターメタデータをロードします
        /// </summary>
        /// <param name="avatarObject">対象のアバターオブジェクト</param>
        /// <returns>ロードされたアバターメタデータを返却する。GlobalObjectId が無効な場合は null を返却する。</returns>
        public static AvatarMetadata LoadMetadata(GameObject avatarObject)
        {
            if (avatarObject == null)
            {
                return null;
            }

            GlobalObjectId id = GlobalObjectId.GetGlobalObjectIdSlow(avatarObject);
            if (id.Equals(default))
            {
                return null;
            }

            return LoadMetadata(id);
        }

        /// <summary>
        /// 指定された GlobalObjectId に対応するアバターメタデータをロードします
        /// </summary>
        /// <param name="id">対象のGlobalObjectId</param>
        /// <returns>ロードされたアバターメタデータを返却する。GlobalObjectId が無効な場合は null を返却する。</returns>
        public static AvatarMetadata LoadMetadata(GlobalObjectId id)
        {
            var path = GetMetadataPath(id);
            if (string.IsNullOrEmpty(path))
            {
                return null;
            }

            return AssetDatabase.LoadAssetAtPath<AvatarMetadata>(path);
        }

        /// <summary>
        /// 指定された GlobalObjectId に対応するアバターメタデータをロードまたは新規作成します
        /// </summary>
        /// <param name="id">対象のGlobalObjectId</param>
        /// <param name="createdNew">新規作成された場合にtrue</param>
        /// <returns>ロードまたは作成されたアバターメタデータを返却する。GlobalObjectId が無効な場合は null を返却する。</returns>
        public static AvatarMetadata LoadOrCreateMetadata(GameObject avatarObject)
        {
            if (avatarObject == null)
            {
                return null;
            }

            GlobalObjectId globalId = GlobalObjectId.GetGlobalObjectIdSlow(avatarObject);
            if (globalId.Equals(default))
            {
                return null;
            }

            string metadataPath = GetMetadataPath(globalId);
            if (string.IsNullOrEmpty(metadataPath))
            {
                return null;
            }

            return AssetDatabase.LoadAssetAtPath<AvatarMetadata>(metadataPath);
        }

        /// <summary>
        /// 指定された GlobalObjectId に対応するアバターメタデータをロードまたは新規作成します
        /// </summary>
        /// <param name="id">対象のGlobalObjectId</param>
        /// <param name="createdNew">新規作成された場合にtrue</param>
        /// <returns>ロードまたは作成されたアバターメタデータを返却する。GlobalObjectId が無効な場合は null を返却する。</returns>
        public static AvatarMetadata LoadOrCreateMetadata(GlobalObjectId id, out bool createdNew)
        {
            createdNew = false;
            var path = GetMetadataPath(id);
            if (string.IsNullOrEmpty(path))
            {
                return null;
            }

            AvatarMetadata data = AssetDatabase.LoadAssetAtPath<AvatarMetadata>(path);
            if (data == null)
            {
                data = ScriptableObject.CreateInstance<AvatarMetadata>();
                data.avatarGlobalObjectId = id.ToString();

                FolderUtil.CreateAvatarMetadataFolder();

                try
                {
                    AssetDatabase.CreateAsset(data, path);
                    AssetDatabase.SaveAssets();
                    AssetDatabase.Refresh();
                    createdNew = true;
                    Debug.Log($"Created new metadata asset at: {path}");
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"Failed to create asset {path}: {ex.Message}");
                    return null;
                }

                data = AssetDatabase.LoadAssetAtPath<AvatarMetadata>(path);
                if (data == null)
                {
                    Debug.LogError($"Failed to load newly created asset at {path}");
                }
            }

            return data;
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

        public static AvatarMetadata CreateMetadata(GameObject avatarObject)
        {
            if (avatarObject == null)
            {
                return null;
            }

            var vrcDescriptor = avatarObject.GetComponent<VRCAvatarDescriptor>();
            if (vrcDescriptor == null)
            {
                Debug.LogWarning($"GameObject '{avatarObject.name}' does not have a VRCAvatarDescriptor component. Cannot create metadata.");
                return null;
            }


            GlobalObjectId globalId = GlobalObjectId.GetGlobalObjectIdSlow(avatarObject);
            if (globalId.Equals(default))
            {
                return null;
            }

            string metadataPath = GetMetadataPath(globalId);
            if (string.IsNullOrEmpty(metadataPath))
            {
                return null;
            }

            if (AssetDatabase.LoadAssetAtPath<AvatarMetadata>(metadataPath) != null)
            {
                Debug.LogWarning($"Metadata already exists for {avatarObject.name} at {metadataPath}. Returning existing one.");
                return AssetDatabase.LoadAssetAtPath<AvatarMetadata>(metadataPath);
            }

            AvatarMetadata newMetadata = ScriptableObject.CreateInstance<AvatarMetadata>();
            newMetadata.avatarGlobalObjectId = globalId.ToString();
            newMetadata.comment = "";
            newMetadata.tags = new List<string>();

            try
            {
                AssetDatabase.CreateAsset(newMetadata, metadataPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                Debug.Log($"Created metadata for {avatarObject.name} at {metadataPath}");
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

        public static bool DeleteMetadata(GameObject avatarObject)
        {
            if (avatarObject == null)
            {
                return false;
            }

            GlobalObjectId globalId = GlobalObjectId.GetGlobalObjectIdSlow(avatarObject);
            if (globalId.Equals(default))
            {
                return false;
            }

            string metadataPath = GetMetadataPath(globalId);
            if (string.IsNullOrEmpty(metadataPath))
            {
                return false;
            }

            // アセットが存在するかどうかで判断
            if (AssetDatabase.LoadAssetAtPath<AvatarMetadata>(metadataPath) == null)
            {
                Debug.LogWarning($"Metadata not found for {avatarObject.name} at {metadataPath}. Cannot delete.");
                return false;
            }

            bool result = AssetDatabase.DeleteAsset(metadataPath);
            if (result)
            {
                Debug.Log($"Deleted metadata for {avatarObject.name} at {metadataPath}");

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
            else
            {
                Debug.LogError($"Failed to delete metadata for {avatarObject.name} at {metadataPath}");
            }

            return result;
        }
    }
}

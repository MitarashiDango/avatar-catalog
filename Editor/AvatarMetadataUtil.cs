using UnityEditor;
using UnityEngine;

namespace MitarashiDango.AvatarCatalog
{
    public class AvatarMetadataUtil : MonoBehaviour
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

            return $"{AvatarMetadataFolderPath}/{id.assetGUID}_{id.targetObjectId}_{id.targetPrefabId}.asset";
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
    }
}

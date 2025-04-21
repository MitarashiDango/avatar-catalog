using UnityEditor;
using UnityEngine;

namespace MitarashiDango.AvatarCatalog
{
    public class AvatarMetadataUtil : MonoBehaviour
    {
        public const string MetadataBaseFolder = "Assets/Avatar Catalog User Data/AvatarMetadata";

        /// <summary>
        /// GlobalObjectIdからメタデータの期待されるアセットパスを取得します。
        /// </summary>
        /// <param name="id">対象のGlobalObjectId</param>
        /// <returns>アセットパス。無効なGoidの場合はnull。</returns>
        public static string GetMetadataPath(GlobalObjectId id)
        {
            if (id.Equals(default(GlobalObjectId)) && id.identifierType == 0)
            {
                Debug.LogWarning("Attempted to get metadata path for an invalid GlobalObjectId.");
                return null;
            }

            var safeFileName = id.ToString().Replace(":", "_").Replace("/", "_");
            return $"{MetadataBaseFolder}/{safeFileName}.asset";
        }

        /// <summary>
        /// 指定されたGlobalObjectIdに対応するAvatarMetadataをロードします。
        /// </summary>
        /// <param name="id">対象のGlobalObjectId</param>
        /// <returns>ロードされたAvatarMetadata。存在しない場合はnull。</returns>
        public static AvatarMetadata LoadMetadata(GlobalObjectId id)
        {
            var path = GetMetadataPath(id);
            if (string.IsNullOrEmpty(path)) return null;
            return AssetDatabase.LoadAssetAtPath<AvatarMetadata>(path);
        }

        /// <summary>
        /// 指定されたGlobalObjectIdに対応するAvatarMetadataをロードまたは新規作成します。
        /// </summary>
        /// <param name="id">対象のGlobalObjectId</param>
        /// <param name="createdNew">新規作成された場合にtrue</param>
        /// <returns>ロードまたは作成されたAvatarMetadata。Goidが無効な場合はnull。</returns>
        public static AvatarMetadata LoadOrCreateMetadata(GlobalObjectId id, out bool createdNew)
        {
            createdNew = false;
            var path = GetMetadataPath(id);
            if (string.IsNullOrEmpty(path)) return null;

            AvatarMetadata data = AssetDatabase.LoadAssetAtPath<AvatarMetadata>(path);
            if (data == null)
            {
                data = ScriptableObject.CreateInstance<AvatarMetadata>();
                data.avatarGlobalObjectId = id.ToString();

                DirectoryUtil.CreateAvatarMetadataFolder();

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
        /// AvatarMetadataアセットへの変更を保存します。
        /// </summary>
        /// <param name="metadata">保存するAvatarMetadataインスタンス</param>
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

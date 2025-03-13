using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace MitarashiDango.AvatarCatalog
{
    [Serializable]
    public class AvatarThumbnailCacheDatabase : ScriptableObject
    {
        public static string ASSET_FILE_PATH = "Assets/AvatarCatalog/AvatarThumbnailCacheDatabase.asset";

        [SerializeField]
        private List<CachedAvatarThumbnail> _thumbnails = new List<CachedAvatarThumbnail>();
        public Dictionary<string, string> thumbnails { get; private set; }

        public Texture2D StoreAvatarThumbnailImage(GlobalObjectId avatarGlobalObjectId, Texture2D texture)
        {
            string path;
            var gid = avatarGlobalObjectId.ToString();
            var cachedAvatarThumbnail = _thumbnails.Where(thumbnail => thumbnail.avatarGlobalObjectId == gid).FirstOrDefault();
            if (cachedAvatarThumbnail != null)
            {
                path = AssetDatabase.GUIDToAssetPath(cachedAvatarThumbnail.thumbnailGlobalObjectId);
                if (path != "")
                {
                    // ファイルが存在する場合、上書きする
                    File.WriteAllBytes(path, texture.EncodeToPNG());
                    AssetDatabase.SaveAssets();
                    return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                }
            }

            path = $"Assets/AvatarCatalog/Cache/AvatarThumbnail/thumbnail_{avatarGlobalObjectId.assetGUID}_{avatarGlobalObjectId.targetObjectId}_{avatarGlobalObjectId.targetPrefabId}_{new DateTimeOffset(DateTime.UtcNow).ToUnixTimeSeconds()}.png";

            File.WriteAllBytes(path, texture.EncodeToPNG());
            AssetDatabase.ImportAsset(path);

            if (cachedAvatarThumbnail == null)
            {
                _thumbnails.Add(new CachedAvatarThumbnail(gid, AssetDatabase.AssetPathToGUID(path)));
            }
            else
            {
                var index = _thumbnails.FindIndex(t => t.avatarGlobalObjectId == gid);
                if (index >= 0)
                {
                    _thumbnails[index].thumbnailGlobalObjectId = AssetDatabase.AssetPathToGUID(path);
                }
                else
                {
                    _thumbnails.Add(new CachedAvatarThumbnail(gid, AssetDatabase.AssetPathToGUID(path)));
                }
            }

            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        public Texture2D TryGetCachedAvatarThumbnailImage(GlobalObjectId avatarGlobalObjectId)
        {
            var gid = avatarGlobalObjectId.ToString();
            var index = _thumbnails.FindIndex(t => t.avatarGlobalObjectId == gid);
            if (index < 0)
            {
                return null;
            }

            var path = AssetDatabase.GUIDToAssetPath(_thumbnails[index].thumbnailGlobalObjectId);
            if (path == "")
            {
                return null;
            }

            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        public void RemoveAvatarThumbnailImage(GlobalObjectId avatarGlobalObjectId)
        {
            var gid = avatarGlobalObjectId.ToString();
            var index = _thumbnails.FindIndex(t => t.avatarGlobalObjectId == gid);
            if (index < 0)
            {
                return;
            }

            var path = AssetDatabase.GUIDToAssetPath(_thumbnails[index].thumbnailGlobalObjectId);
            if (path != "")
            {
                AssetDatabase.DeleteAsset(path);
            }

            _thumbnails.RemoveAt(index);
        }

        public bool IsExists(GlobalObjectId avatarGlobalObjectId)
        {
            var gid = avatarGlobalObjectId.ToString();
            var index = _thumbnails.FindIndex(t => t.avatarGlobalObjectId == gid);
            if (index < 0)
            {
                return false;
            }

            var path = AssetDatabase.GUIDToAssetPath(_thumbnails[index].thumbnailGlobalObjectId);
            if (path == "")
            {
                return false;
            }

            return true;
        }

        public void Save()
        {
            var asset = AssetDatabase.LoadAssetAtPath<AvatarThumbnailCacheDatabase>(ASSET_FILE_PATH);
            if (!asset)
            {
                AssetDatabase.CreateAsset(this, ASSET_FILE_PATH);
            }
            else
            {
                EditorUtility.CopySerialized(this, asset);
            }

            AssetDatabase.Refresh();
        }

        public static AvatarThumbnailCacheDatabase CreateOrLoad()
        {
            var asset = AssetDatabase.LoadAssetAtPath<AvatarThumbnailCacheDatabase>(ASSET_FILE_PATH);
            if (!asset)
            {
                asset = ScriptableObject.CreateInstance<AvatarThumbnailCacheDatabase>();
                AssetDatabase.CreateAsset(asset, ASSET_FILE_PATH);
            }

            return asset;
        }

        [Serializable]
        public class CachedAvatarThumbnail
        {
            public string avatarGlobalObjectId;
            public string thumbnailGlobalObjectId;

            public CachedAvatarThumbnail(string avatarGlobalObjectId, string thumbnailGlobalObjectId)
            {
                this.avatarGlobalObjectId = avatarGlobalObjectId;
                this.thumbnailGlobalObjectId = thumbnailGlobalObjectId;
            }
        }
    }
}

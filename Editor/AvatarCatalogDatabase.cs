using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace MitarashiDango.AvatarCatalog
{
    [Serializable]
    public class AvatarCatalogDatabase : ScriptableObject
    {
        public static readonly string AssetFilePath = "Assets/Avatar Catalog User Data/AvatarCatalogDatabase.asset";

        [SerializeField]
        private List<AvatarCatalogEntry> _avatars = new List<AvatarCatalogEntry>();

        public List<AvatarCatalogEntry> avatars
        {
            get => _avatars;
            set
            {
                _avatars = value;
                EditorUtility.SetDirty(this);
            }
        }

        public bool IsExists(GameObject avatarRootObject)
        {
            return IsExists(GlobalObjectId.GetGlobalObjectIdSlow(avatarRootObject));
        }

        public bool IsExists(GlobalObjectId avatarGlobalObjectId)
        {
            return IsExists(avatarGlobalObjectId.ToString());
        }

        public bool IsExists(string avatarGlobalObjectId)
        {
            return _avatars.Exists(a => a.avatarGlobalObjectId == avatarGlobalObjectId);
        }

        public void Set(AvatarCatalogEntry entry)
        {
            var index = avatars.FindIndex(t => t.avatarGlobalObjectId == entry.avatarGlobalObjectId);
            if (index != -1)
            {
                avatars[index] = entry;
            }
            else
            {
                avatars.Add(entry);
            }

            EditorUtility.SetDirty(this);
        }

        public AvatarCatalogEntry Get(GameObject avatarRootObject)
        {
            return Get(GlobalObjectId.GetGlobalObjectIdSlow(avatarRootObject));
        }

        public AvatarCatalogEntry Get(GlobalObjectId avatarGlobalObjectId)
        {
            return Get(avatarGlobalObjectId.ToString());
        }

        public AvatarCatalogEntry Get(string avatarGlobalObjectId)
        {
            return _avatars.Where(a => a.avatarGlobalObjectId == avatarGlobalObjectId).FirstOrDefault();
        }

        public Dictionary<string, AvatarCatalogEntry> GetMappedAvatarCatalogEntries()
        {
            var dic = new Dictionary<string, AvatarCatalogEntry>();

            foreach (var entry in avatars)
            {
                dic.Add(entry.avatarGlobalObjectId, entry);
            }

            return dic;
        }

        public static void Save(AvatarCatalogDatabase asset)
        {
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssetIfDirty(asset);
        }

        public static AvatarCatalogDatabase LoadOrCreateFile()
        {
            var asset = Load();
            if (asset != null)
            {
                return asset;
            }

            // フォルダー作成
            FolderUtil.CreateUserDataFolder();

            asset = CreateInstance<AvatarCatalogDatabase>();
            AssetDatabase.CreateAsset(asset, AssetFilePath);

            return asset;
        }

        public static AvatarCatalogDatabase Load()
        {
            var asset = AssetDatabase.LoadAssetAtPath<AvatarCatalogDatabase>(AssetFilePath);
            if (asset != null)
            {
                return asset;
            }

            return null;
        }

        public static bool IsDatabaseFileExists()
        {
            return AssetDatabase.LoadAssetAtPath<AvatarCatalogDatabase>(AssetFilePath) != null;
        }

        [Serializable]
        public class AvatarCatalogEntry
        {
            public string avatarGlobalObjectId;
            public string avatarObjectName;
            public SceneAsset sceneAsset;
            public string thumbnailImageGuid = "";
            public string avatarMetadataGuid = "";

            public AvatarCatalogEntry()
            {
            }

            public AvatarCatalogEntry(AvatarCatalogEntry ace)
            {
                avatarGlobalObjectId = ace.avatarGlobalObjectId;
                avatarObjectName = ace.avatarObjectName;
                sceneAsset = ace.sceneAsset;
                thumbnailImageGuid = ace.thumbnailImageGuid;
                avatarMetadataGuid = ace.avatarMetadataGuid;
            }
        }
    }
}

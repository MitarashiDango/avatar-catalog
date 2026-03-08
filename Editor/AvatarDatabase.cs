using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace MitarashiDango.AvatarCatalog
{
    [Serializable]
    public class AvatarDatabase : ScriptableObject
    {
        public static readonly string AssetFilePath = "Assets/Avatar Catalog User Data/AvatarDatabase.asset";

        [SerializeField]
        private List<AvatarDatabaseEntry> _avatars = new List<AvatarDatabaseEntry>();

        public List<AvatarDatabaseEntry> avatars
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

        public void Set(AvatarDatabaseEntry entry)
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

        public AvatarDatabaseEntry Get(GameObject avatarRootObject)
        {
            return Get(GlobalObjectId.GetGlobalObjectIdSlow(avatarRootObject));
        }

        public AvatarDatabaseEntry Get(GlobalObjectId avatarGlobalObjectId)
        {
            return Get(avatarGlobalObjectId.ToString());
        }

        public AvatarDatabaseEntry Get(string avatarGlobalObjectId)
        {
            return _avatars.FirstOrDefault(a => a.avatarGlobalObjectId == avatarGlobalObjectId);
        }

        public Dictionary<string, AvatarDatabaseEntry> GetMappedAvatarCatalogEntries()
        {
            var dic = new Dictionary<string, AvatarDatabaseEntry>();

            foreach (var entry in avatars)
            {
                dic.Add(entry.avatarGlobalObjectId, entry);
            }

            return dic;
        }

        public static void Save(AvatarDatabase asset)
        {
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssetIfDirty(asset);
        }

        public static AvatarDatabase LoadOrCreateFile()
        {
            var asset = Load();
            if (asset != null)
            {
                return asset;
            }

            // フォルダー作成
            FolderUtil.CreateUserDataFolder();

            asset = CreateInstance<AvatarDatabase>();
            AssetDatabase.CreateAsset(asset, AssetFilePath);

            return asset;
        }

        public static AvatarDatabase Load()
        {
            var asset = AssetDatabase.LoadAssetAtPath<AvatarDatabase>(AssetFilePath);
            if (asset != null)
            {
                return asset;
            }

            return null;
        }

        public static bool IsDatabaseFileExists()
        {
            return AssetDatabase.LoadAssetAtPath<AvatarDatabase>(AssetFilePath) != null;
        }

        [Serializable]
        public class AvatarDatabaseEntry
        {
            public string avatarGlobalObjectId;
            public string avatarObjectName;
            public string sceneAssetGuid;
            public string thumbnailImageGuid = "";

            public AvatarDatabaseEntry()
            {
            }

            public AvatarDatabaseEntry(AvatarDatabaseEntry ace)
            {
                avatarGlobalObjectId = ace.avatarGlobalObjectId;
                avatarObjectName = ace.avatarObjectName;
                sceneAssetGuid = ace.sceneAssetGuid;
                thumbnailImageGuid = ace.thumbnailImageGuid;
            }
        }
    }
}

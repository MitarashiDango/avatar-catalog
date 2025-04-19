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
        private static AvatarCatalogDatabase _instance;

        public static string ASSET_FILE_PATH = "Assets/Avatar Catalog User Data/AvatarCatalogDatabase.asset";

        [SerializeField]
        private List<Avatar> _avatars = new List<Avatar>();

        protected AvatarCatalogDatabase() : base()
        {
        }

        public static AvatarCatalogDatabase Instance
        {
            get
            {
                if (_instance == null)
                {
                    LoadOrNewInstance();
                }

                return _instance;
            }
        }

        public List<Avatar> avatars
        {
            get => _avatars;
            private set => _avatars = value;
        }

        public bool IsExists(GameObject avatar)
        {
            var gid = GlobalObjectId.GetGlobalObjectIdSlow(avatar).ToString();
            return _avatars.Where(a => a.globalObjectId == gid).Count() > 0;
        }

        public void Clear()
        {
            _avatars.Clear();
        }

        public void AddAvatar(Avatar avatar)
        {
            if (_avatars.Where(a => a.globalObjectId == avatar.globalObjectId).Count() > 0)
            {
                return;
            }

            _avatars.Add(avatar);
        }

        public static void Save(bool withSaveAssets = false)
        {
            var asset = AssetDatabase.LoadAssetAtPath<AvatarCatalogDatabase>(ASSET_FILE_PATH);
            if (asset == null)
            {
                AssetDatabase.CreateAsset(Instance, ASSET_FILE_PATH);
            }
            else
            {
                EditorUtility.CopySerialized(Instance, asset);
                EditorUtility.SetDirty(asset);
            }

            if (withSaveAssets)
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
        }

        public static void LoadOrNewInstance()
        {
            Load();

            if (_instance != null)
            {
                return;
            }

            _instance = CreateInstance<AvatarCatalogDatabase>();
        }

        public static void LoadOrCreateFile()
        {
            Load();

            if (_instance != null)
            {
                return;
            }

            var asset = ScriptableObject.CreateInstance<AvatarCatalogDatabase>();
            AssetDatabase.CreateAsset(asset, ASSET_FILE_PATH);

            _instance = asset;
        }

        public static void Load()
        {
            var asset = AssetDatabase.LoadAssetAtPath<AvatarCatalogDatabase>(ASSET_FILE_PATH);
            if (asset != null)
            {
                _instance = asset;
                return;
            }

            _instance = null;
        }

        public static bool IsDatabaseFileExists()
        {
            var asset = AssetDatabase.LoadAssetAtPath<AvatarCatalogDatabase>(ASSET_FILE_PATH);
            return asset != null;
        }

        [Serializable]
        public class Avatar
        {
            public string avatarName;
            public SceneAsset sceneAsset;
            public string globalObjectId;

            public Avatar(SceneAsset sceneAsset, GameObject avatar)
            {
                this.sceneAsset = sceneAsset;
                avatarName = avatar.name;
                globalObjectId = GlobalObjectId.GetGlobalObjectIdSlow(avatar).ToString();
            }
        }
    }
}

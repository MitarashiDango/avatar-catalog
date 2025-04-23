using System;
using UnityEditor;
using UnityEngine;

namespace MitarashiDango.AvatarCatalog
{
    [Serializable]
    public class Preferences : ScriptableObject
    {
        private static Preferences _instance;

        public static readonly float AvatarCatalogMinItemSize = 128;
        public static readonly float AvatarCatalogMaxItemSize = 256;
        public static readonly float DefaultAvatarCatalogMaxItemSize = 160;

        public static string ASSET_FILE_PATH = "Assets/Avatar Catalog User Data/Preferences.asset";

        [SerializeField]
        private float _avatarCatalogItemSize = DefaultAvatarCatalogMaxItemSize;

        protected Preferences() : base()
        {
        }

        public static Preferences Instance
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

        public float avatarCatalogItemSize
        {
            get => _avatarCatalogItemSize;
            set
            {
                if (value < AvatarCatalogMinItemSize)
                {
                    _avatarCatalogItemSize = AvatarCatalogMinItemSize;
                    return;
                }

                if (value > AvatarCatalogMaxItemSize)
                {
                    _avatarCatalogItemSize = AvatarCatalogMaxItemSize;
                    return;
                }

                _avatarCatalogItemSize = value;
            }
        }

        public static void Save(bool withSaveAssets = false)
        {
            var asset = AssetDatabase.LoadAssetAtPath<Preferences>(ASSET_FILE_PATH);
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

            _instance = CreateInstance<Preferences>();
        }

        public static void LoadOrCreateFile()
        {
            Load();

            if (_instance != null)
            {
                return;
            }

            var asset = ScriptableObject.CreateInstance<Preferences>();
            AssetDatabase.CreateAsset(asset, ASSET_FILE_PATH);

            _instance = asset;
        }

        public static void Load()
        {
            var asset = AssetDatabase.LoadAssetAtPath<Preferences>(ASSET_FILE_PATH);
            if (asset != null)
            {
                _instance = asset;
                return;
            }

            _instance = null;
        }
    }
}

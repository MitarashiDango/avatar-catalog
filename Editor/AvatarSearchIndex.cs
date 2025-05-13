using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace MitarashiDango.AvatarCatalog
{
    public class AvatarSearchIndex : ScriptableObject
    {
        public static readonly string AssetFilePath = "Assets/Avatar Catalog User Data/AvatarSearchIndex.asset";

        [SerializeField]
        private List<AvatarSearchIndexEntry> _entries = new List<AvatarSearchIndexEntry>();

        public List<AvatarSearchIndexEntry> entries
        {
            get => _entries;
            set
            {
                _entries = value;
                EditorUtility.SetDirty(this);
            }
        }

        public AvatarSearchIndexEntry Get(GlobalObjectId avatarGlobalObjectId)
        {
            return Get(avatarGlobalObjectId.ToString());
        }

        public AvatarSearchIndexEntry Get(string avatarGlobalObjectId)
        {
            return entries.Where(entry => entry.avatarGlobalObjectId == avatarGlobalObjectId).FirstOrDefault();
        }

        public void Set(AvatarSearchIndexEntry avatarSearchIndexEntry)
        {
            var index = entries.FindIndex(entry => entry.avatarGlobalObjectId == avatarSearchIndexEntry.avatarGlobalObjectId);
            if (index >= 0)
            {
                entries[index] = avatarSearchIndexEntry;
            }
            else
            {
                entries.Add(avatarSearchIndexEntry);
            }

            EditorUtility.SetDirty(this);
        }

        public IEnumerable<AvatarSearchIndexEntry> Find(string[] searchWords)
        {
            return entries.Where(entry => entry.IsMatch(searchWords));
        }

        public List<string> GetGlobalObjectIds(string[] searchWords)
        {
            return Find(searchWords).Select(entry => entry.avatarGlobalObjectId).ToList();
        }

        public static AvatarSearchIndex LoadOrCreateFile()
        {
            var asset = Load();
            if (asset != null)
            {
                return asset;
            }

            // フォルダー作成
            FolderUtil.CreateUserDataFolder();

            asset = CreateInstance<AvatarSearchIndex>();
            AssetDatabase.CreateAsset(asset, AssetFilePath);

            return asset;
        }

        /// <summary>
        /// アバター検索用インデックスをロードします
        /// </summary>
        /// <returns>ロードされたアバター検索用インデックス, ファイルが存在しない場合はnull</returns>
        public static AvatarSearchIndex Load()
        {
            var asset = AssetDatabase.LoadAssetAtPath<AvatarSearchIndex>(AssetFilePath);
            if (asset != null)
            {
                return asset;
            }

            return null;
        }

        /// <summary>
        /// アバター検索用インデックスを保存します
        /// </summary>
        /// <param name="asset">保存対象のアバター検索用インデックス情報</param>
        public static void Save(AvatarSearchIndex asset)
        {
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssetIfDirty(asset);
            AssetDatabase.Refresh();
        }

        [Serializable]
        public class AvatarSearchIndexEntry
        {
            public string avatarGlobalObjectId;

            [SerializeField]
            private List<string> _values;

            public AvatarSearchIndexEntry()
            {
                avatarGlobalObjectId = "";
                _values = new List<string>();
            }

            public AvatarSearchIndexEntry(string avatarGlobalObjectId, List<string> values)
            {
                this.avatarGlobalObjectId = avatarGlobalObjectId;

                _values = new List<string>();
                SetValues(values);
            }

            public List<string> Values
            {
                get => _values;
                set => SetValues(value);
            }

            public bool IsMatch(string[] searchWords)
            {
                foreach (var searchWord in searchWords)
                {
                    if (searchWord != "" && !IsMatch(searchWord))
                    {
                        return false;
                    }
                }

                return true;
            }

            public bool IsMatch(string searchWord)
            {
                return _values.Exists(value => value.Contains(searchWord));
            }

            private void SetValues(List<string> values)
            {
                _values = values
                    .Where(v => !string.IsNullOrEmpty(v))
                    .Distinct()
                    .Select(value => value.ToLower())
                    .ToList();
            }
        }
    }
}

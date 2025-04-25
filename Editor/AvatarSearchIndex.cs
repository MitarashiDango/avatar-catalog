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

        public List<AvatarSearchIndexEntry> entries = new List<AvatarSearchIndexEntry>();

        public List<AvatarSearchIndexEntry> Find(string[] searchWords)
        {
            return entries.Where(entry => entry.IsMatch(searchWords)).ToList();
        }

        public void Add(string globalObjectId, List<string> values)
        {
            var entry = new AvatarSearchIndexEntry();
            entry.globalObjectId = globalObjectId;
            entry.values = values.Distinct().Select(value => value.ToLower()).ToList();
            entries.Add(entry);
        }

        public List<string> GetGlobalObjectIds(string[] searchWords)
        {
            return Find(searchWords).Select(entry => entry.globalObjectId).ToList();
        }

        /// <summary>
        /// アバター検索用インデックスをロードします
        /// </summary>
        /// <returns>ロードされたアバター検索用インデックス, ファイルが存在しない場合は新しいインスタンス</returns>
        public static AvatarSearchIndex LoadOrNewInstance()
        {
            var asset = Load();
            if (asset != null)
            {
                return asset;
            }

            return CreateNewInstance();
        }

        /// <summary>
        /// 新しいアバター検索用インデックスのインスタンスを返却します
        /// </summary>
        /// <returns>アバター検索用インデックス</returns>
        public static AvatarSearchIndex CreateNewInstance()
        {
            return CreateInstance<AvatarSearchIndex>();
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
        /// <param name="asi">保存対象のアバター検索用インデックス情報</param>
        /// <param name="withSaveAssets">アセット保存処理を呼び出すかどうか</param>
        public static void Save(AvatarSearchIndex asi, bool withSaveAssets = false)
        {
            var asset = AssetDatabase.LoadAssetAtPath<AvatarSearchIndex>(AssetFilePath);
            if (asset == null)
            {
                // フォルダー作成
                FolderUtil.CreateUserDataFolder();

                AssetDatabase.CreateAsset(asi, AssetFilePath);
            }
            else
            {
                EditorUtility.CopySerialized(asi, asset);
                EditorUtility.SetDirty(asset);
            }

            if (withSaveAssets)
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
        }

        [Serializable]
        public class AvatarSearchIndexEntry
        {
            public string globalObjectId = "";
            public List<string> values = new List<string>();

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
                return values.Exists(value => value.Contains(searchWord));
            }
        }
    }
}

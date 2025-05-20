using System;
using System.Collections.Generic;
using UnityEngine;

namespace MitarashiDango.AvatarCatalog.ExternalInterface
{
    [Serializable]
    public class BoothItem
    {
        public BoothCategory category;

        public string description;

        public string id;

        public string name;

        [SerializeField]
        private string published_at;
        public string publishedAt
        {
            get => published_at;
            set => published_at = value;
        }

        public BoothShop shop;

        public List<BoothTag> tags;

        public string url;
    }
}

﻿using Azure;

namespace Cosmos.Copilot.Models
{
    public class Product
    {
        public string id { get; set; }
        public string categoryId { get; set; }
        public string categoryName { get; set; }
        public string sku { get; set; }
        public string name { get; set; }
        public string description { get; set; }
        public double price { get; set; }
        public List<Tag> tags { get; set; }
        public float[]? vectors { get; set; }

        public Product(string id, string categoryId, string categoryName, string sku, string name, string description, double price, List<Tag> tags, float[]? vectors = null)
        {
            this.id = id;
            this.categoryId = categoryId;
            this.categoryName = categoryName;
            this.sku = sku;
            this.name = name;
            this.description = description;
            this.price = price;
            this.tags = tags;
            this.vectors = vectors;
        }

    }
    public class Tag
    {
        public string id { get; set; }
        public string name { get; set; }

        public Tag(string id, string name)
        {
            this.id = id;
            this.name = name;
        }
    }
}

﻿using Azure;
using Microsoft.Extensions.VectorData;

namespace Cosmos.Copilot.Models
{
    public class Product
    {
        [VectorStoreRecordKey]
        public string id { get; set; }
        [VectorStoreRecordData]
        public string categoryId { get; set; }
        [VectorStoreRecordData]
        public string categoryName { get; set; }
        [VectorStoreRecordData]
        public string sku { get; set; }
        [VectorStoreRecordData]
        public string name { get; set; }
        [VectorStoreRecordData]
        public string description { get; set; }
        [VectorStoreRecordData]
        public double price { get; set; }
        //[VectorStoreRecordData]
        public List<Tag> tags { get; set; }
        //[VectorStoreRecordData]
        public List<Review> reviews { get; set; }

        [VectorStoreRecordVector(Dimensions: 1536, DistanceFunction: DistanceFunction.CosineSimilarity, IndexKind: IndexKind.DiskAnn)]
        public ReadOnlyMemory<float>? vectors { get; set; }

        public Product(string id, string categoryId, string categoryName, string sku, string name, string description, double price, List<Tag> tags, List<Review> reviews, ReadOnlyMemory<float>? vectors = null)
        {
            this.id = id;
            this.categoryId = categoryId;
            this.categoryName = categoryName;
            this.sku = sku;
            this.name = name;
            this.description = description;
            this.price = price;
            this.tags = tags;
            this.reviews = reviews;
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

    public class Review
    {
        public string customer { get; set; }
        public int rating { get; set; }
        public string review { get; set; }

        public Review(string customer, int rating, string review)
        {
            this.customer = customer;
            this.rating = rating;
            this.review = review;
        }
    }
}
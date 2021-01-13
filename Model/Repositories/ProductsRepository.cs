using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using MongoDB.Driver.GridFS;
using store_service.App.Model;
using store_service.Helpers;
using Data = store_service.App.Model.Data;

namespace store_service.App.Repository.Internal {

    public class ProductsRepository : RepositoryBase, IProductsRepository {

        private readonly IMongoCollection<Data.Product> _products;
        private readonly IMongoCollection<Data.Inventory> _inventory;
        public ProductsRepository (IConfiguration configuration, IHostingEnvironment env) : base (configuration, env) {
            var database = _client.GetDatabase ("bowties_store2_db");

            _products = database.GetCollection<Data.Product> ("products");
            _inventory = database.GetCollection<Data.Inventory> ("inventory");

            var field = new StringFieldDefinition<Data.Inventory>("VariationId");
            var indexDefinition = new IndexKeysDefinitionBuilder<Data.Inventory>().Ascending(field);
            var indexModel = new CreateIndexModel<Data.Inventory>(indexDefinition);
            _inventory.Indexes.CreateOne(indexModel);
        }

        public async Task CreateProduct (Data.Product product) {
            product.Created = DateTime.UtcNow;
            await _products.InsertOneAsync (product);
        }
        public async Task UpdateProduct (Data.Product product) {
            var filter = Builders<Data.Product>.Filter.Eq (i => i.Id, product.Id);
            product.LastChange = DateTime.UtcNow;
            await _products.ReplaceOneAsync (filter, product);
        }
        public async Task UpdateProductImages(ObjectId id, List<Data.ImageDesc> images)
        {
            await _products.UpdateOneAsync(x => x.Id == id,
                Builders<Data.Product>.Update
                .Set(x => x.Images, images)
                .CurrentDate(x=>x.LastChange));
        }
        public async Task DeleteProduct (ObjectId productId) {
            var filter = Builders<Data.Product>.Filter.Eq (i => i.Id, productId);
            await _products.UpdateOneAsync (filter, Builders<Data.Product>.Update.Set (i => i.IsArchived, true));

            await _inventory.DeleteManyAsync (
                Builders<Data.Inventory>.Filter.And (
                    Builders<Data.Inventory>.Filter.Eq (i => i.ProductId, productId),
                    Builders<Data.Inventory>.Filter.Eq (i => i.State, InventoryState.Available)));

        }

        public async Task<string[]> GetAllColors()
        {
            var resultColor = await (await _products.DistinctAsync(i=>i.Properties.Color, 
                Builders<Data.Product>.Filter.Ne(i => i.IsArchived, true)))
                .ToListAsync();
            var resultSecColor = await (await _products.DistinctAsync(i => i.Properties.SecondaryColor,
                Builders<Data.Product>.Filter.Ne(i => i.IsArchived, true)))
                .ToListAsync();
            return resultColor.Union(resultSecColor)
                .Where(i=>!string.IsNullOrWhiteSpace(i))
                .Distinct()
                .OrderBy(i=>i)
                .ToArray();
        }

        public async Task<Data.Product[]> GetAllProducts (ObjectId? groupId, bool filterUnavailable, bool filterArchived=true) {
            var filter = Builders<Data.Product>.Filter.Empty;

            if(filterArchived)
                filter = Builders<Data.Product>.Filter.Ne(i => i.IsArchived, true);

            if (groupId.HasValue) {
                filter = Builders<Data.Product>.Filter.And (filter,
                    Builders<Data.Product>.Filter.Eq (i => i.GroupId, groupId));
            }
            var result = (await (await _products.FindAsync (filter)).ToListAsync ()).ToArray ();

            if (filterUnavailable)
                //TODO заменить монго
                return result.Where(i => i.Variations.Any(k => !k.IsArchived && k.CanBeOrdered)).ToArray();
            else
                return result;
        }

        public async Task<Data.Product[]> GetAllProducts(ObjectId[] groupId, bool filterUnavailable)
        {
            var filter = Builders<Data.Product>.Filter.Ne(i => i.IsArchived, true);

            filter = Builders<Data.Product>.Filter.And(filter,
                    Builders<Data.Product>.Filter.In(i => i.GroupId, groupId));

            var result = (await (await _products.FindAsync(filter)).ToListAsync()).ToArray();

            if (filterUnavailable)
                //TODO заменить монго
                return result.Where(i => i.Variations.Any(k => !k.IsArchived && k.CanBeOrdered)).ToArray();
            else
                return result;
        }

        public async Task<Data.Product> GetProduct (ObjectId id) {
            var result = await (await _products.FindAsync (Builders<Data.Product>.Filter.Eq (x => x.Id, id))).ToListAsync ();
            if (result.Count == 1)
                return result[0];
            else
                return null;
        }

        public async Task<int> GetInventoryAvailable (ObjectId variationId) {
            var filter = Builders<Data.Inventory>.Filter.And (
                Builders<Data.Inventory>.Filter.Eq (i => i.VariationId, variationId),
                Builders<Data.Inventory>.Filter.Eq (i => i.State, InventoryState.Available)
            );

            var result = await _inventory.CountDocumentsAsync (filter);
            return (int) result;
        }
        public async Task<int[]> GetInventoryAvailable(ObjectId[] variationId)
        {
            var filter = Builders<Data.Inventory>.Filter.And(
                Builders<Data.Inventory>.Filter.In(i => i.VariationId, variationId),
                Builders<Data.Inventory>.Filter.Eq(i => i.State, InventoryState.Available)
            );

            var available = await (await _inventory.FindAsync(filter)).ToListAsync();
            var result = new List<int>();
            foreach(var id in variationId)
            {
                result.Add(available.Where(i => i.VariationId == id).Count());
            }
            return result.ToArray();
        }
        public async Task UpdateVariationInventory (ObjectId productId, ObjectId variationId, int inventoryCount) {
            var productInventoryAll = Builders<Data.Inventory>.Filter.And (
                Builders<Data.Inventory>.Filter.Eq (i => i.ProductId, productId),
                Builders<Data.Inventory>.Filter.Eq (i => i.VariationId, variationId),
                Builders<Data.Inventory>.Filter.Eq (i => i.State, InventoryState.Available)
            );

            var inventoryIdsList = await (await _inventory.FindAsync (productInventoryAll)).ToListAsync ();

            if (inventoryIdsList.Count > inventoryCount) {
                //удаляем лишний инвентарь
                var idsToFree = inventoryIdsList.Where (x => x.State == InventoryState.Available)
                    .Take (inventoryIdsList.Count - inventoryCount).Select (x => x.Id).ToArray ();
                var result = await _inventory.DeleteManyAsync (
                    Builders<Data.Inventory>.Filter.And (
                        Builders<Data.Inventory>.Filter.In (x => x.Id, idsToFree),
                        Builders<Data.Inventory>.Filter.Eq (x => x.State, InventoryState.Available),
                        Builders<Data.Inventory>.Filter.Eq (i => i.ProductId, productId),
                        Builders<Data.Inventory>.Filter.Eq (i => i.VariationId, variationId)
                    ));
            } else {
                //добавляем недостающий новый инвентарь
                var newInventory = new Data.Inventory[inventoryCount - inventoryIdsList.Count];
                for (int i = 0; i < newInventory.Length; i++) {
                    newInventory[i] = new Data.Inventory () {
                        ProductId = productId,
                        VariationId = variationId,
                        State = InventoryState.Available
                    };
                }
                if (newInventory.Length > 0)
                    await _inventory.InsertManyAsync (newInventory);
            }
            //TODO т.к. обновление оптимистичное - проверить что мы обновили именно столько сколько нужно и повторить при необх
        }

        public async Task<GroupCount[]> GetAllInventoryAvailable()
        {
            //TODO версия AggregateAsync
            var result = await (_inventory.Aggregate()
                .Match(i=>i.State==InventoryState.Available)
                .Group(new BsonDocument { { "_id", "$VariationId" }, { "Count", new BsonDocument { { "$sum", 1 } } } })).ToListAsync();
            //_inventory.AggregateAsync(Aggregates PipelineDefinitionBuilder.AppendStage<Data.Inventory, VariationCount, VariationCount>()
            return result.Select(i => BsonSerializer.Deserialize<GroupCount>(i)).ToArray(); ;
        }

        public async Task SetProductOrder(ObjectId id, int order)
        {
            var filter = Builders<Data.Product>.Filter.Eq(i => i.Id, id);
            await _products.UpdateOneAsync(filter, Builders<Data.Product>.Update.Set(i => i.Order, order));
        }
    }
}
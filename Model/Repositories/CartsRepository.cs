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
using store_service.Helpers;
using Data = store_service.App.Model.Data;

namespace store_service.App.Repository.Internal
{

    public class CartsRepository : RepositoryBase, ICartsRepository
    {

        private readonly IMongoCollection<Data.Cart> _carts;
        public CartsRepository(IConfiguration configuration, IHostingEnvironment env)
        : base(configuration, env)
        {
            var database = _client.GetDatabase("bowties_store2_db");

            _carts = database.GetCollection<Data.Cart>("carts");
        }

        public async Task<Data.Cart> GetUserCart(ObjectId userId)
        {

            var filter = Builders<Data.Cart>.Filter.Eq(i => i.UserId, userId);
            var target = await (await _carts.FindAsync(filter)).ToListAsync();
            if (target.Count < 1)
            {
                var result = await _carts.FindOneAndUpdateAsync(filter,
                    Builders<Data.Cart>.Update
                        .Set(x => x.UserId, userId)
                        .Set(x => x.LastUpdate, DateTime.UtcNow),
                    new FindOneAndUpdateOptions<Data.Cart> { IsUpsert = true, ReturnDocument = ReturnDocument.After });
                return result;
            }
            else
                return target[0];
        }

        public async Task CreateCart(ObjectId userId)
        {
            await _carts.InsertOneAsync(new Data.Cart { UserId = userId, LastUpdate = DateTime.UtcNow });
        }

        public async Task MergeCarts(ObjectId tempSessionUserId, ObjectId userId)
        {
            var result = await (await _carts.FindAsync(
                Builders<Data.Cart>.Filter.In(x => x.UserId, new ObjectId[] { tempSessionUserId, userId }))).ToListAsync();
            if (result.Count == 2)
            {
                var tempCart = result.FirstOrDefault(i => i.UserId == tempSessionUserId);
                if (tempCart != null && tempCart.Items != null && tempCart.Items.Count > 0)
                {
                    var cart = result.First(i => i.UserId == userId);
                    var updateResult = await _carts.UpdateOneAsync(
                        Builders<Data.Cart>.Filter.And(
                            Builders<Data.Cart>.Filter.Eq(i => i.UserId, userId),
                            Builders<Data.Cart>.Filter.Eq(i => i.LastUpdate, cart.LastUpdate)),
                        Builders<Data.Cart>.Update
                        .Set(i => i.Items, tempCart.Items)
                        .CurrentDate(i => i.LastUpdate));

                    updateResult = await _carts.UpdateOneAsync(
                        Builders<Data.Cart>.Filter.And(
                            Builders<Data.Cart>.Filter.Eq(i => i.UserId, tempSessionUserId),
                            Builders<Data.Cart>.Filter.Eq(i => i.LastUpdate, tempCart.LastUpdate)),
                        Builders<Data.Cart>.Update
                        .Set(i => i.Items, new List<Data.CartItem>())
                        .CurrentDate(i => i.LastUpdate));
                }
            }
        }

        public async Task<bool> UpdateCart(Data.Cart cart)
        {
            var filter = Builders<Data.Cart>.Filter.And(
                Builders<Data.Cart>.Filter.Eq(i => i.UserId, cart.UserId)//,
                                                                         //Builders<Data.Cart>.Filter.Eq(i => i.LastUpdate, cart.LastUpdate)
                );
            var result = await _carts.ReplaceOneAsync(filter, cart);
            return result.ModifiedCount == 1;
        }

        public async Task<List<Data.Cart>> GetAllCarts()
        {
            var result = await _carts.FindAsync((i) => true);
            return await result.ToListAsync();
        }
    }
}
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

    public class GroupsRepository : RepositoryBase, IGroupsRepository {
        private readonly IMongoCollection<Data.Group> _groups;
        public GroupsRepository (IConfiguration configuration, IHostingEnvironment env) : base (configuration, env) {
            var database = _client.GetDatabase ("bowties_store2_db");

            _groups = database.GetCollection<Data.Group> ("groups");
        }

        public async Task CreateGroup (Data.Group group) {
            group.Created = DateTime.UtcNow;
            await _groups.InsertOneAsync (group);
        }

        public async Task DeleteGroup (ObjectId groupId) {
            var filter = Builders<Data.Group>.Filter.Eq (i => i.Id, groupId);
            await _groups.UpdateOneAsync (filter, Builders<Data.Group>.Update.Set (i => i.IsArchived, true));
        }

        public async Task<List<Data.Group>> GetAllGroups (bool filterArchivedGroups = true) {
            List<Data.Group> result;
            if (filterArchivedGroups)
            {
                result = await (await _groups.FindAsync(
                    Builders<Data.Group>.Filter.Eq(x => x.IsArchived, false))).ToListAsync();
            }else
            {
                result = await (await _groups.FindAsync(x=>true)).ToListAsync();
            }
            foreach(var g in result)
                FilterArchived(g);
            return result;
        }

        public async Task<Data.Group> GetGroup (ObjectId id) {
            var result = await (await _groups.FindAsync (
                Builders<Data.Group>.Filter.Eq (x => x.Id, id))).ToListAsync ();

            if (result.Count == 1) {
                FilterArchived(result[0]);
                return result[0];
            } else
                return null;
        }
        private void FilterArchived (Data.Group group) {
            foreach(var p in group.VariationParameters)
            {
                p.Values = p.Values.Where(i => !i.IsArchived).ToList();
            }
        }

        public async Task UpdateGroup (Data.Group group) {
            var filter = Builders<Data.Group>.Filter.Eq (i => i.Id, group.Id);
            group.LastChange = DateTime.UtcNow;
            await _groups.ReplaceOneAsync (filter, group);
        }

        public async Task SetGroupOrder(ObjectId id, int order)
        {
            var filter = Builders<Data.Group>.Filter.Eq(i => i.Id, id);
            await _groups.UpdateOneAsync(filter, Builders<Data.Group>.Update.Set(i => i.Order, order));
        }

        public async Task UpdateGroupImages(ObjectId id, Data.ImageDesc image)
        {
            await _groups.UpdateOneAsync(x => x.Id == id,
                Builders<Data.Group>.Update
                .Set(x => x.Image, image)
                .CurrentDate(x => x.LastChange));
        }
    }
}
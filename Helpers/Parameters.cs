using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using MongoDB.Bson;
using MongoDB.Driver;
using store_service.App.Repository.Internal;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace store_service.Helpers
{
    public interface IGlobalParameters
    {
        GlobalParameters.Parameters Get();

    }
    public class GlobalParameters: RepositoryBase, IGlobalParameters
    {
        public class Parameters
        {
            public ObjectId Id { get; set; }
            //TODO шаблоны уведомлений и т.д.
        }

        private readonly IMongoCollection<Parameters> _parameters;

        public GlobalParameters(IConfiguration configuration, IHostingEnvironment env)
        : base(configuration, env)
        {
            var database = _client.GetDatabase("bowties_store2_db");
            _parameters = database.GetCollection<Parameters>("parameters");
            Update();
        }

        private DateTime LastUpdate = DateTime.UtcNow;
        private Parameters ActualValues { get; set; } = new Parameters();

        private void Update()
        {
            var values = _parameters.Find(i=>true).ToList();
            if(values.Count==1)
            {
                ActualValues = values.First();
            }
        }

        public Parameters Get()
        {
            if ((DateTime.UtcNow - LastUpdate).TotalMinutes > 10)
                Update();
            return ActualValues;
        }
    }
}

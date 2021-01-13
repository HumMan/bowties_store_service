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
       public class RepositoryBase
    {
        private readonly IConfiguration Configuration;
        protected readonly MongoClient _client;       
        public RepositoryBase(IConfiguration configuration, IHostingEnvironment env)
        {
            Configuration = configuration;

            var settings = new MongoClientSettings();

            if (env.IsProduction())
            {
                settings.Server = MongoServerAddress.Parse(Environment.GetEnvironmentVariable("MONGODB_SERVER_PORT_27017_TCP").Replace("tcp://", ""));
                settings.Credential = MongoCredential.CreateCredential("admin",
                    Configuration["MongoUser"],
                    Configuration["MongoPassword"]);
            }                
            else
            {
                settings.Server=MongoServerAddress.Parse(Configuration["MongoDbConnection"]);
            }

            _client = new MongoClient(settings);
        }
    }
}
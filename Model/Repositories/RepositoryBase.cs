using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using MongoDB.Driver.GridFS;
using store_service.App.Model.Data;
using store_service.Helpers;
using Data = store_service.App.Model.Data;

namespace store_service.App.Repository.Internal
{
       public class RepositoryBase
    {
        private readonly IConfiguration _configuration;
        protected readonly MongoClient _client;       
        public RepositoryBase(IConfiguration configuration, IHostingEnvironment env)
        {
            _configuration = configuration;
            var connString = _configuration["MongoConnectionString"];
                
            var settings = MongoClientSettings.FromConnectionString(connString);
            _client = new MongoClient(settings);
        }
    }
}
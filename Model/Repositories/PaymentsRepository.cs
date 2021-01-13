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

namespace store_service.App.Repository.Internal {
    
    public class PaymentsRepository : RepositoryBase, IPaymentsRepository {

        private readonly IMongoCollection<Data.PaymentNotify> _paymentNotifies;
        public PaymentsRepository (IConfiguration configuration, IHostingEnvironment env) : base (configuration, env) {
            var database = _client.GetDatabase ("bowties_store2_db");

            _paymentNotifies = database.GetCollection<Data.PaymentNotify> ("payment_notifies");
        }

        public async Task InsertPaymentNotify (Data.PaymentNotify notify) {
            await _paymentNotifies.InsertOneAsync (notify);
        }
    }
}
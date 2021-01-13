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

    public class UserTicketsBase : RepositoryBase
    {
        private readonly IMongoCollection<Data.UserTicket> _tickets;
        public UserTicketsBase(string name,
            IConfiguration configuration,
             IHostingEnvironment env)
        : base(configuration, env)
        {
            var database = _client.GetDatabase("bowties_store2_db");

            _tickets = database.GetCollection<Data.UserTicket>(name);

            var options = new CreateIndexOptions() { Unique = false };
            var field = new StringFieldDefinition<Data.UserTicket>("Email");
            var indexDefinition = new IndexKeysDefinitionBuilder<Data.UserTicket>().Ascending(field);
            var indexModel = new CreateIndexModel<Data.UserTicket>(indexDefinition, options);
            _tickets.Indexes.CreateOne(indexModel);
        }

        public async Task<Data.UserTicket> TryTicket(string tokenHash)
        {
            var ticket = await _tickets.FindOneAndUpdateAsync(
                x => !x.IsUsed && x.TokenHash == tokenHash && x.ExpirationDate > DateTime.UtcNow,
                Builders<Data.UserTicket>.Update.Set(x => x.IsUsed, true));
            return ticket;
        }

        public async Task<bool> IsValidTicket(string tokenHash)
        {
            var count = await _tickets.CountDocumentsAsync(
                x => !x.IsUsed && x.TokenHash == tokenHash && x.ExpirationDate > DateTime.UtcNow);
            return count > 0;
        }

        public async Task CreateTicket(ObjectId userId, string _email, string token, DateTime expirationDate)
        {
            var email = _email.ToLower();
            //отменяем все предыдущие токены пользователя - в принципе можно удалять
            await _tickets.UpdateManyAsync(i => i.Email == email && !i.IsUsed, Builders<Data.UserTicket>.Update.Set(i => i.IsUsed, true));
            //создаём новый
            await _tickets.InsertOneAsync(
                new Data.UserTicket
                {
                    Email = email,
                    ExpirationDate = expirationDate,
                    IsUsed = false,
                    TokenHash = token
                });
        }
    }

    public class EmailVerifyTickets : UserTicketsBase, IEmailVerifyTicketRepository
    {
        public EmailVerifyTickets(IConfiguration configuration, IHostingEnvironment env)
        : base("email_verify_tickets", configuration, env)
        {
        }
    }

    public class PasswordResetTickets : UserTicketsBase, IPasswordResetTicketRepository
    {
        public PasswordResetTickets(IConfiguration configuration, IHostingEnvironment env)
        : base("password_reset_tickets", configuration, env)
        {
        }
    }
}
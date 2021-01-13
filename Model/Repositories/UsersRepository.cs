using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using MongoDB.Bson;
using MongoDB.Driver;
using store_service.App.Model;
using store_service.Helpers;
using System;
using System.Threading.Tasks;
using Data = store_service.App.Model.Data;

namespace store_service.App.Repository.Internal
{
    public class UsersRepository : RepositoryBase, IUsersRepository
    {

        private readonly IMongoCollection<Data.User> _users;
        public UsersRepository(IConfiguration configuration,
            IHostingEnvironment env) : base(configuration, env)
        {
            var database = _client.GetDatabase("bowties_store2_db");
            _users = database.GetCollection<Data.User>("users");

            var options = new CreateIndexOptions() { Unique = true };
            var field = new StringFieldDefinition<Data.User>("Email");
            var indexDefinition = new IndexKeysDefinitionBuilder<Data.User>().Ascending(field);
            var indexModel = new CreateIndexModel<Data.User>(indexDefinition, options);
            _users.Indexes.CreateOne(indexModel);
        }

        public async Task<Data.User> FindUser(string _email)
        {
            var email = _email.ToLower();
            var target = await (await _users.FindAsync(x => x.Email == email)).ToListAsync();
            if (target.Count == 1)
                return target[0];
            else
                return null;
        }
        public async Task<Data.User> FindUser(ObjectId id)
        {
            var target = await (await _users.FindAsync(x => x.Id == id)).ToListAsync();
            if (target.Count == 1)
                return target[0];
            else
                return null;
        }
        public async Task<Data.User> TryLogin(string _email, string password)
        {
            var email = _email.ToLower();
            var user = await FindUser(email);
            if (user != null && user.RegisteredUser!=null)
            {
                if (UserHashing.TryLogin(password, user.RegisteredUser.PasswordSalt, user.RegisteredUser.PasswordHash))
                    return user;
            }
            return null;
        }
        public async Task<Data.User> TryLogin(ObjectId userId, string password)
        {
            var user = await FindUser(userId);
            if (user != null && user.RegisteredUser != null)
            {
                if (UserHashing.TryLogin(password, user.RegisteredUser.PasswordSalt, user.RegisteredUser.PasswordHash))
                    return user;
            }
            return null;
        }
        public async Task<Data.User> CreateTempUser()
        {
            var id = ObjectId.GenerateNewId();
            var newUser = new Data.User
            {
                Id = id,
                Email = id.ToString(),
                Created = DateTime.UtcNow
            };
            await _users.InsertOneAsync(newUser);
            return newUser;
        }

        public async Task<Data.User> RegisterUser(string userName, string password, string _email)
        {
            var email = _email.ToLower();
            var target = await (await _users.FindAsync(x => x.Email == email)).ToListAsync();

            Data.User result = null;
            if (target.Count == 0)
            {

                UserHashing.CreatePassHashAndSalt(password, out var salt, out var passHash);

                var newUser = new Data.User
                {
                    Email = email,                   
                    
                    Created = DateTime.UtcNow,
                    RegisteredUser = new Data.RegisteredUser()
                    {
                        Role = UserRole.Regular,
                        Name = userName,
                        PasswordHash = passHash,
                        PasswordSalt = Convert.ToBase64String(salt),
                    }
                };
                await _users.InsertOneAsync(newUser);
                result = newUser;
            }
            return result;
        }

        public async Task ChangeUserPassword(string email, ObjectId userId, string newPassword)
        {
            UserHashing.CreatePassHashAndSalt(newPassword, out var salt, out var passHash);

            await _users.UpdateOneAsync(x => x.Id == userId,
                Builders<Data.User>.Update
                .Set(x => x.RegisteredUser.PasswordHash, passHash)
                .Set(x => x.RegisteredUser.PasswordSalt, Convert.ToBase64String(salt))
                .CurrentDate(x=>x.RegisteredUser.PasswordLastChange)
                );
        }

        public async Task<Data.User[]> GetUsers()
        {
            var result = await (await _users.FindAsync(_ => true
            //, new FindOptions<Data.User> {
            //    Projection = Builders<Model.Data.User>.Projection
            //    .Include (x => x.Id)
            //    .Include (x => x.Name)
            //    .Include (x => x.Role)
            //    .Include (x => x.Email)
            //}
            )).ToListAsync();
            return result.ToArray();
        }
        //public async Task UpdateUser(Data.User user)
        //{
        //    var filter = Builders<Data.User>.Filter.And(
        //        Builders<Data.User>.Filter.Eq(i => i.Id, user.Id)
        //    );

        //    var result = await _users.UpdateOneAsync(filter,
        //        Builders<Data.User>.Update
        //        .Set(x => x.RegisteredUser.Name, user.RegisteredUser.Name)
        //        .Set(x => x.Email, user.Email.ToLower())
        //    );
        //}

        public async Task SetUserEmailValid(ObjectId userId, bool value)
        {
            await _users.UpdateOneAsync(x => x.Id == userId,
                Builders<Data.User>.Update
                .Set(x => x.RegisteredUser.IsEmailVerified, value)
            );
        }

        public async Task UpdateCheckoutParams(ObjectId userId, CheckoutParameters parameters)
        {
            await _users.UpdateOneAsync(x => x.Id == userId,
                Builders<Data.User>.Update
                .Set(x => x.CheckoutParameters, parameters)
            );
        }
    }
}
using Microsoft.Extensions.Configuration;
using MongoDB.Bson;
using store_service.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace store_service.Helpers
{
    public static class UserHashing
    {
        const int TokenLength = 64;

        public static bool TryLogin(string password, string passwordSalt, string passwordHash)
        {
            return Hashing.ValidateValue(password,Convert.FromBase64String(passwordSalt),
                    passwordHash);
        }
        public static void CreatePassHashAndSalt(string password, out byte[] salt, out string passHash)
        {
            salt = Hashing.GenerateRandomValue(Hashing.HashLength);
            passHash = Hashing.CreateHash(password,salt);
        }
        public static void CreateTokenAndHash(byte[] salt, out string token, out string tokenHash)
        {
            token = Convert.ToBase64String(Hashing.GenerateRandomValue(TokenLength));
            tokenHash = Hashing.CreateHash(token, salt);
        }

        public static bool IsValidTicket(byte[] salt, string token, string tokenHash)
        {
            return Hashing.ValidateValue(token, salt, tokenHash);
        }
    }
}

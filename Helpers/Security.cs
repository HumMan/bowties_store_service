using System;
using System.Security;
using System.Security.Cryptography;
using System.Text;

namespace store_service.Helpers
{
    public static class Hashing
    {
        private const int IterCount = 1000;
        //public const int SaltLength = 32;
        public const int HashLength = 64;
        public static byte[] GenerateRandomValue(int length)
        {
            using (var provider = new RNGCryptoServiceProvider())
            {
                var result = new byte[length];
                provider.GetBytes(result);
                return result;
            }
        }
        public static bool ValidateValue(string value, byte[] salt, string hash)
        {
            return SlowEqual(CreateHash(value, salt), hash);
        }
        public static string CreateHash(string source, byte[] salt)
        {
            using (var provider = new Rfc2898DeriveBytes(source, salt, IterCount))
            {
                var bytes = provider.GetBytes(HashLength);                
                return System.Convert.ToBase64String(bytes);
            }
        }
        private static bool SlowEqual(byte[] left, byte[] right)
        {
            int diff = left.Length ^ right.Length;
            for (int i = 0; i < left.Length && i < right.Length; i++)
            {
                diff |= left[i] ^ right[i];
            }
            return diff == 0;
        }
        private static bool SlowEqual(string left, string right)
        {
            int diff = left.Length ^ right.Length;
            for (int i = 0; i < left.Length && i < right.Length; i++)
            {
                diff |= left[i] ^ right[i];
            }
            return diff == 0;
        }
    }    
}
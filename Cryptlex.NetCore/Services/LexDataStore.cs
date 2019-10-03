using System;
using System.Collections.Generic;
using Cryptlex.NetCore.Contracts;

namespace Cryptlex.NetCore.Services
{
    public class LexDataStore
    {
//        public static string AppVersion;
//        public static readonly string ClientVersion = "3.0.0-unity";
        private readonly IPersistence _persistence;
        public LexDataStore(IPersistence persistence)
        {
            _persistence = persistence;
        }
        
        private static string GetDataKey(string productId, string key)
        {
            return LexEncryptionService.Sha256(productId + key);
        }

        public void SaveValue(string productId, string key, string value)
        {
            _persistence?.Store(GetDataKey(productId, key), value);
        }

        public string GetValue(string productId, string key)
        {
            return _persistence?.Read(GetDataKey(productId, key));
        }

        public void ResetValue(string productId, string key)
        {
            SaveValue(productId, key, String.Empty);
        }

        public void Reset(string productId)
        {
            ResetValue(productId, LexConstants.KEY_LICENSE_KEY);
            ResetValue(productId, LexConstants.KEY_ACTIVATION_JWT);
        }
    }
}
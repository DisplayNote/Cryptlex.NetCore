using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace Cryptlex.NetCore.Services
{
    class LexReleaseService
    {
        static public (bool available, bool error) UpdateAvailable(string platform, string productId, string version, string key)
        {
            var httpService = new LexHttpService();
            
            try
            {
                var response = httpService.CheckForUpdate(platform, productId, version, key);
                var json = response.Content.ReadAsStringAsync().Result;
                dynamic obj = JsonConvert.DeserializeObject(json);
                return ((bool)obj.published, false);
            }
            catch (Exception)
            {
                return (false, true);
            }
        }

        static public (string url, string version, bool error) LatestRelease(string platform, string productId, string key)
        {
            var httpService = new LexHttpService();

            try
            {
                var response = httpService.GetLatestRelease(platform, productId, key);
                var json = response.Content.ReadAsStringAsync().Result;
                dynamic obj = JsonConvert.DeserializeObject(json);
                
                return ((string)obj.files[0].url, (string)obj.version, false);
            }
            catch (Exception)
            {
                return (null, null, true);
            }
        }
    }
}

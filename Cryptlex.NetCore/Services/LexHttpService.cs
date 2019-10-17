using System;
using System.Text;
using System.Threading.Tasks;
using System.Net.Http;
using System.Net.Mime;

namespace Cryptlex.NetCore.Services
{

    public class LexHttpService
    {
        private const string _apiUrl = "https://api.cryptlex.com/v3";

        public LexHttpService()
        {
        }

        public HttpResponseMessage CreateActivation(string postData)
        {
            return Post($"{_apiUrl}/activations", postData);
        }

        public HttpResponseMessage UpdateActivation(string activationId, string postData)
        {
            return Patch($"{_apiUrl}/activations/{activationId}", postData);
        }

        public HttpResponseMessage DeleteActivation(string activationId)
        {
            return Delete($"{_apiUrl}/activations/{activationId}", null);
        }

        public HttpResponseMessage CheckForUpdate(string platform, string productId, string version, string key)
        {
            return Get($"{_apiUrl}/releases/update?platform={platform}&productId={productId}&version={version}&key={key}");
        }

        public HttpResponseMessage GetLatestRelease(string platform, string productId, string key)
        {
            return Get($"{_apiUrl}/releases/latest?platform={platform}&productId={productId}&key={key}");
        }

        private HttpResponseMessage Get(string url)
        {
            var client = new HttpClient();
            var request = new HttpRequestMessage
            {
                Method = HttpMethod.Get,
                RequestUri = new Uri(url)
            };
            
            return client.SendAsync(request).Result;
        }

        private HttpResponseMessage Post(string url, string data)
        {
            var client = new HttpClient();
            var request = new HttpRequestMessage
            {
                Method = HttpMethod.Post,
                RequestUri = new Uri(url)
            };
            request.Content = new StringContent(data, Encoding.UTF8, LexConstants.JsonContentType);
            return client.SendAsync(request).Result;
        }

        private HttpResponseMessage Patch(string url, string data)
        {
            var client = new HttpClient();
            var request = new HttpRequestMessage
            {
                Method = new HttpMethod(LexConstants.HttpMethodPatch),
                RequestUri = new Uri(url)
            };
            request.Content = new StringContent(data, Encoding.UTF8, LexConstants.JsonContentType);
            return client.SendAsync(request).Result;
        }

        private HttpResponseMessage Delete(string url, string data)
        {
            var client = new HttpClient();
            var request = new HttpRequestMessage
            {
                Method = HttpMethod.Delete,
                RequestUri = new Uri(url)
            };
            return client.SendAsync(request).Result;
        }
    }
}
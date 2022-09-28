using Bot.Models.Booru;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace Bot.Services.Booru
{

    public abstract class BooruService<TResponse, T> : IBooruProvider<T>
    {

        private readonly CredentialsService _credentialsService;
        private readonly CredentialsEntry _credentials;
        private readonly HttpClient _clientAsync;

        public abstract string Name { get; }


        public BooruService(CredentialsService credentials)
        {
            _credentialsService = credentials;
            _credentials = _credentialsService.Credentials.FirstOrDefault(c => c.Name.Equals(GetCredentialsKey(), StringComparison.OrdinalIgnoreCase));
            _clientAsync = InternalCreateClient();
        }


        public async Task<IEnumerable<T>> SearchAsync(int limit, int page, params string[] searchTags)
        {
            var tags = EncodeText(string.Join(" ", searchTags));
            var url = GetSearchString(limit, page, tags);
            using var getResponse = await _clientAsync.GetAsync(url);
            var response = await getResponse.Content.ReadAsStringAsync();
            if (response is null) return Enumerable.Empty<T>();
            var responseObject = JsonConvert.DeserializeObject<TResponse>(response);
            var x = ConvertResponseAsEnumerable(responseObject);
            if (x is null) x = Enumerable.Empty<T>();
            return x;
        }

        private HttpClient InternalCreateClient()
        {
            var c = new HttpClient();
            var userAgent = new ProductHeaderValue(GetUserAgent, "1.0");
            c.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            c.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue(userAgent));
            c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic",
                Convert.ToBase64String(Encoding.ASCII.GetBytes($"{_credentials.Username}:{_credentials.ApiKey}")));
            return BuildHttpClient(c);
        }

        protected abstract string GetCredentialsKey();

        protected abstract string GetSearchString(int limit, int page, string searchTags);

        protected abstract IEnumerable<T> ConvertResponseAsEnumerable(TResponse response);


        /// <summary>Applies any additional transformations on the built HttpClient</summary>
        protected virtual HttpClient BuildHttpClient(HttpClient client) => client;

        /// <summary>Gets the User Agent that will be used by the API</summary>
        protected virtual string GetUserAgent => _credentials is null ?
            $"The-Erector" :
            $"The-Erector-by-{_credentials.Username}";

        /// <summary>Gets the current credentials for the API</summary>
        protected internal CredentialsEntry GetCredentials() => _credentials;



        protected string EncodeText(string text) => WebUtility.UrlEncode(text);

    }

}

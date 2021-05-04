using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Http;

namespace OpenRa.Server
{
    public class HttpClientFactory : IHttpClientFactory
    {
        private readonly ConcurrentDictionary<string, HttpClient> HttpClientCache = new ConcurrentDictionary<string, HttpClient>();

        public HttpClient GetForHost(Uri uri)
        {
            var key = $"{uri.Scheme}://{uri.DnsSafeHost}:{uri.Port}";

            return HttpClientCache.GetOrAdd(key, k =>
            {
                var client = new HttpClient();

                var sp = ServicePointManager.FindServicePoint(uri);
                sp.ConnectionLeaseTimeout = 60 * 1000; // 1 minute

                return client;
            });
        }
    }
}
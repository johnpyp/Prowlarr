using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NLog;
using NzbDrone.Common.Cache;
using NzbDrone.Common.Http;
using NzbDrone.Common.Http.Dispatchers;
using NzbDrone.Common.TPL;
using NzbDrone.Core.IndexerProxies;
using NzbDrone.Core.ThingiProvider;

namespace NzbDrone.Core.Indexers
{
    public interface IIndexerHttpClient : IHttpClient
    {
        Task<HttpResponse> ExecuteAsync(HttpRequest request, ProviderDefinition definition);
        HttpResponse Get(HttpRequest request, ProviderDefinition definition);
    }

    public class IndexerHttpClient : HttpClient, IIndexerHttpClient
    {
        private readonly IIndexerProxyFactory _indexerProxyFactory;
        public IndexerHttpClient(IIndexerProxyFactory indexerProxyFactory,
            IEnumerable<IHttpRequestInterceptor> requestInterceptors,
            ICacheManager cacheManager,
            IRateLimitService rateLimitService,
            IHttpDispatcher httpDispatcher,
            Logger logger)
            : base(requestInterceptors, cacheManager, rateLimitService, httpDispatcher, logger)
        {
            _indexerProxyFactory = indexerProxyFactory;
        }

        public async Task<HttpResponse> ExecuteAsync(HttpRequest request, ProviderDefinition definition)
        {
            var proxies = _indexerProxyFactory.GetAvailableProviders();
            IIndexerProxy selectedProxy = null;

            foreach (var proxy in proxies)
            {
                if (definition.Tags.Intersect(proxy.Definition.Tags).Any())
                {
                    selectedProxy = proxy;
                    request = selectedProxy.PreRequest(request);
                    break;
                }
            }

            return ProcessResponse(await ExecuteAsync(request), selectedProxy);
        }

        public HttpResponse Get(HttpRequest request, ProviderDefinition definition)
        {
            var proxies = _indexerProxyFactory.GetAvailableProviders();
            IIndexerProxy selectedProxy = null;

            foreach (var proxy in proxies)
            {
                if (definition.Tags.Intersect(proxy.Definition.Tags).Any())
                {
                    selectedProxy = proxy;
                    request = selectedProxy.PreRequest(request);
                    break;
                }
            }

            return ProcessResponse(Get(request), selectedProxy);
        }

        private HttpResponse ProcessResponse(HttpResponse response, IIndexerProxy selectedProxy)
        {
            if (selectedProxy != null)
            {
                response = selectedProxy.PostResponse(response);
            }

            return response;
        }
    }
}

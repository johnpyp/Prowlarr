using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using FluentValidation.Results;
using Newtonsoft.Json;
using NLog;
using NzbDrone.Common.Http;
using NzbDrone.Common.Serializer;
using NzbDrone.Core.Indexers;

namespace NzbDrone.Core.Applications.Mylar
{
    public interface IMylarV3Proxy
    {
        MylarIndexer AddIndexer(MylarIndexer indexer, MylarSettings settings);
        List<MylarIndexer> GetIndexers(MylarSettings settings);
        MylarIndexer GetIndexer(string indexerName, MylarProviderType indexerType, MylarSettings settings);
        void RemoveIndexer(string indexerName, MylarProviderType indexerType, MylarSettings settings);
        MylarIndexer UpdateIndexer(MylarIndexer indexer, MylarSettings settings);
        ValidationFailure TestConnection(MylarSettings settings);
    }

    public class MylarV1Proxy : IMylarV3Proxy
    {
        private readonly IHttpClient _httpClient;
        private readonly Logger _logger;

        public MylarV1Proxy(IHttpClient httpClient, Logger logger)
        {
            _httpClient = httpClient;
            _logger = logger;
        }

        public MylarStatus GetStatus(MylarSettings settings)
        {
            var request = BuildRequest(settings, "/api", "getVersion", HttpMethod.GET);
            return Execute<MylarStatus>(request);
        }

        public List<MylarIndexer> GetIndexers(MylarSettings settings)
        {
            var request = BuildRequest(settings, "/api", "listProviders", HttpMethod.GET);

            var response = Execute<MylarIndexerResponse>(request);

            if (!response.Success)
            {
                throw new Exception(string.Format("Mylar Error - Code {0}: {1}", response.Error.Code, response.Error.Message));
            }

            var indexers = new List<MylarIndexer>();

            var torIndexers = response.Data.Torznabs;
            torIndexers.ForEach(i => i.Type = MylarProviderType.Torznab);

            var nzbIndexers = response.Data.Newznabs;
            nzbIndexers.ForEach(i => i.Type = MylarProviderType.Newznab);

            indexers.AddRange(torIndexers);
            indexers.AddRange(nzbIndexers);
            indexers.ForEach(i => i.Altername = i.Name);

            return indexers;
        }

        public MylarIndexer GetIndexer(string indexerName, MylarProviderType indexerType, MylarSettings settings)
        {
            var indexers = GetIndexers(settings);

            return indexers.SingleOrDefault(i => i.Name == indexerName && i.Type == indexerType);
        }

        public void RemoveIndexer(string indexerName, MylarProviderType indexerType, MylarSettings settings)
        {
            var parameters = new Dictionary<string, string>
            {
                { "name", indexerName },
                { "providertype", indexerType.ToString().ToLower() }
            };

            var request = BuildRequest(settings, "/api", "delProvider", HttpMethod.GET, parameters);
            CheckForError(Execute<MylarStatus>(request));
        }

        public MylarIndexer AddIndexer(MylarIndexer indexer, MylarSettings settings)
        {
            var parameters = new Dictionary<string, string>
            {
                { "name", indexer.Name },
                { "providertype", indexer.Type.ToString().ToLower() },
                { "host", indexer.Host },
                { "prov_apikey", indexer.Apikey },
                { "enabled", indexer.Enabled.ToString().ToLower() },
                { "categories", indexer.Categories }
            };

            var request = BuildRequest(settings, "/api", "addProvider", HttpMethod.GET, parameters);
            CheckForError(Execute<MylarStatus>(request));
            return indexer;
        }

        public MylarIndexer UpdateIndexer(MylarIndexer indexer, MylarSettings settings)
        {
            var parameters = new Dictionary<string, string>
            {
                { "name", indexer.Name },
                { "providertype", indexer.Type.ToString().ToLower() },
                { "host", indexer.Host },
                { "prov_apikey", indexer.Apikey },
                { "enabled", indexer.Enabled.ToString().ToLower() },
                { "categories", indexer.Categories },
                { "altername", indexer.Altername }
            };

            var request = BuildRequest(settings, "/api", "changeProvider", HttpMethod.GET, parameters);
            CheckForError(Execute<MylarStatus>(request));
            return indexer;
        }

        private void CheckForError(MylarStatus response)
        {
            if (!response.Success)
            {
                throw new Exception(string.Format("Mylar Error - Code {0}: {1}", response.Error.Code, response.Error.Message));
            }
        }

        public ValidationFailure TestConnection(MylarSettings settings)
        {
            try
            {
                var status = GetStatus(settings);

                if (!status.Success)
                {
                    return new ValidationFailure("ApiKey", status.Error.Message);
                }
            }
            catch (HttpException ex)
            {
                _logger.Error(ex, "Unable to send test message");
                return new ValidationFailure("BaseUrl", "Unable to complete application test");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Unable to send test message");
                return new ValidationFailure("", "Unable to send test message");
            }

            return null;
        }

        private HttpRequest BuildRequest(MylarSettings settings, string resource, string command, HttpMethod method, Dictionary<string, string> parameters = null)
        {
            var baseUrl = settings.BaseUrl.TrimEnd('/');

            var requestBuilder = new HttpRequestBuilder(baseUrl).Resource(resource)
                .AddQueryParam("cmd", command)
                .AddQueryParam("apikey", settings.ApiKey);

            if (parameters != null)
            {
                foreach (var param in parameters)
                {
                    requestBuilder.AddQueryParam(param.Key, param.Value);
                }
            }

            var request = requestBuilder.Build();

            request.Headers.ContentType = "application/json";

            request.Method = method;
            request.AllowAutoRedirect = true;

            return request;
        }

        private TResource Execute<TResource>(HttpRequest request)
             where TResource : new()
        {
            var response = _httpClient.Execute(request);

            var results = JsonConvert.DeserializeObject<TResource>(response.Content);

            return results;
        }
    }
}

﻿namespace Ocelot.Provider.Consul
{
    using global::Consul;
    using Infrastructure.Extensions;
    using Logging;
    using ServiceDiscovery.Providers;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Values;

    public class Consul : IServiceDiscoveryProvider
    {
        private readonly ConsulRegistryConfiguration _config;
        private readonly IOcelotLogger _logger;
        private readonly IConsulClient _consul;
        private const string VersionPrefix = "version-";

        public Consul(ConsulRegistryConfiguration config, IOcelotLoggerFactory factory, IConsulClientFactory clientFactory)
        {
            _logger = factory.CreateLogger<Consul>();
            _config = config;
            _consul = clientFactory.Get(_config);
        }

        public async Task<List<Service>> Get()
        {
            var queryResult = await _consul.Health.Service(_config.KeyOfServiceInConsul, string.Empty, true);

            var services = new List<Service>();

            foreach (var serviceEntry in queryResult.Response)
            {
                if (! IsValid(serviceEntry))
                {
                    _logger.LogWarning($"Unable to use service Address: {GetServiceAddress(serviceEntry)} and Port: {serviceEntry.Service.Port} as it is invalid. Address must contain host only e.g. localhost and port must be greater than 0");
                    continue;
                }

                services.Add(BuildService(serviceEntry));
            }

            return services.ToList();
        }

        private Service BuildService(ServiceEntry serviceEntry)
        {
            return new Service(
                serviceEntry.Service.Service,
                new ServiceHostAndPort(GetServiceAddress(serviceEntry), serviceEntry.Service.Port),
                serviceEntry.Service.ID,
                GetVersionFromStrings(serviceEntry.Service.Tags),
                serviceEntry.Service.Tags ?? Enumerable.Empty<string>()
            );
        }

        private static string GetServiceAddress(ServiceEntry serviceEntry)
        {
            return
                string.IsNullOrWhiteSpace(serviceEntry.Service.Address) ||
                serviceEntry.Service.Address.Equals("localhost", StringComparison.InvariantCultureIgnoreCase) ||
                serviceEntry.Service.Address.Equals("127.0.0.1", StringComparison.InvariantCultureIgnoreCase)
                ? serviceEntry.Node.Address
                : serviceEntry.Service.Address;
        }

        private bool IsValid(ServiceEntry serviceEntry)
        {
            if (string.IsNullOrEmpty(GetServiceAddress(serviceEntry)) || serviceEntry.Service.Address.Contains("http://") || serviceEntry.Service.Address.Contains("https://") || serviceEntry.Service.Port <= 0)
            {
                return false;
            }

            return true;
        }

        private string GetVersionFromStrings(IEnumerable<string> strings)
        {
            return strings
                ?.FirstOrDefault(x => x.StartsWith(VersionPrefix, StringComparison.Ordinal))
                .TrimStart(VersionPrefix);
        }
    }
}

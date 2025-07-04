﻿using System.Collections.Concurrent;
using Microsoft.Extensions.Configuration;

namespace Tinkwell.Supervisor.Commands;

sealed class Endpoints(IConfiguration configuration)
{
    public string? Claim(string machineName, string runnerName)
    {
        if (!_endpoints.TryGetValue(machineName, out var endpoints))
        {
            endpoints = new MachineEndpoints(_configuration, machineName);
            _endpoints[machineName] = endpoints;
        }

        return endpoints.ClaimFor(runnerName);
    }

    public string? Query(string runnerName)
    {
        foreach (var  endpoint in _endpoints.Values)
        {
            string? url = endpoint.Query(runnerName);
            if (url is not null)
                return url;
        }

        return null;
    }

    public string? InverseQuery(string url)
    {
        foreach (var endpoint in _endpoints.Values)
        {
            string? runnerName = endpoint.InverseQuery(url);
            if (runnerName is not null)
                return runnerName;
        }

        return null;
    }

    private readonly IConfiguration _configuration = configuration;
    private readonly ConcurrentDictionary<string, MachineEndpoints> _endpoints = new();

    private sealed class MachineEndpoints
    {
        public MachineEndpoints(IConfiguration configuration, string machineName)
        {
            _nextPortToUse = configuration.GetValue("Supervisor:StartingPort", 5000);
            _machineName = machineName;
        }

        public string? ClaimFor(string runnerName)
        {
            lock (_endpoints)
            {
                string url = $"https://{_machineName}:{_nextPortToUse}";
                if (_endpoints.TryAdd(runnerName, url))
                    return (_nextPortToUse++).ToString();

                return null;
            }
        }

        public string? Query(string runnerName)
        {
            lock (_endpoints)
            {
               if (_endpoints.TryGetValue(runnerName, out string? url))
                    return url;

                return null;
            }
        }

        public string? InverseQuery(string url)
        {
            lock (_endpoints)
            {
                foreach (var entry in _endpoints)
                {
                    if (string.Equals(entry.Value, url, StringComparison.InvariantCultureIgnoreCase))
                        return entry.Key;
                }

                return null;
            }
        }

        private readonly Dictionary<string, string> _endpoints = new();
        private readonly string _machineName;
        private int _nextPortToUse;
    }
}
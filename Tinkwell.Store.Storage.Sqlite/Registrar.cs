using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Tinkwell.Bootstrapper.Hosting;
using Tinkwell.Measures.Storage;

namespace Tinkwell.Store.Storage.Sqlite;

sealed class Registrar : IStrategyImplementationResolver
{
    public void ConfigureServices(IConfigurableHost host)
    {
        host.ConfigureServices((_, services) =>
        {
            string dbPath = Path.Combine(HostingInformation.ApplicationDataDirectory, "measures.db");
            var connectionString = host.GetPropertyString("sqlite_connection_string", $"Data source={dbPath}");

            services.AddSingleton(_ => new SqliteConnection(connectionString));
        });
    }

    public string? GetImplementationName(Type interfaceType)
        => GetImplementationType(interfaceType)?.Name;

    public Type? GetImplementationType(Type interfaceType)
        => interfaceType == typeof(IStorage) ? typeof(SqliteStorage) : null;
}

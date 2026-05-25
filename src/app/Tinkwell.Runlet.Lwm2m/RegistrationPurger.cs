using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Tinkwell.Lwm2m.Registration;

namespace Tinkwell.Runlet.Lwm2m;

/// <summary>
/// Periodically removes expired client registrations from the directory
/// (OMA-TS-LightweightM2M_Core-V1_1, Section 5.3.3 — registration
/// lifetime expiration).
/// </summary>
internal sealed class RegistrationPurger : BackgroundService
{
    private static readonly TimeSpan PurgeInterval = TimeSpan.FromMinutes(1);

    private readonly RegistrationDirectory _directory;
    private readonly ILogger _logger;

    public RegistrationPurger(RegistrationDirectory directory, ILogger logger)
    {
        _directory = directory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(PurgeInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            var purged = _directory.PurgeExpired();
            if (purged > 0)
                _logger.LogInformation("Purged {Count} expired LwM2M registration(s)", purged);
        }
    }
}

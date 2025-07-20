namespace Tinkwell.Bootstrapper.GrpcHost;

interface IActivity
{
    Task ConfigureBuilder(IHostApplicationBuilder builder, CancellationToken cancellationToken);

    Task ConfigureApplication(WebApplication app, CancellationToken cancellationToken);
}

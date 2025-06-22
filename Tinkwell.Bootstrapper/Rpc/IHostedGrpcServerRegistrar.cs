namespace Tinkwell.Bootstrapper.Rpc;

public interface IHostedGrpcServerRegistrar
{
    public string ServerName
        => GetType().Name;

    public void ConfigureServices(IGrpcServerHost host);
    public void ConfigureRoutes(IGrpcServerHost host);
}

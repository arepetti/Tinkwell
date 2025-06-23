namespace Tinkwell.Bootstrapper.Rpc;

public interface IHostedGrpcServerRegistrar
{
    public void ConfigureServices(IGrpcServerHost host);
    public void ConfigureRoutes(IGrpcServerHost host);
}

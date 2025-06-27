namespace Tinkwell.Bootstrapper;

public interface IHostedDllRegistrar
{
    public void ConfigureServices(IDllHost host);
}

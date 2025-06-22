namespace Tinkwell.Bootstrapper.GrpcHost;

static class Extensions
{
    public static Task DelegateConfigureServices(this IHostApplicationBuilder builder)
    {
        return RegisterServicesActivity
            .FromBuilder(builder)
            .ConfigureBuilder(builder, CancellationToken.None);
    }

    public static async Task DelegateConfigureRoutes(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        foreach (var activity in scope.ServiceProvider.GetServices<IActivity>())
            await activity.ConfigureApplication(app, CancellationToken.None);
    }
}

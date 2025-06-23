namespace Tinkwell.Bootstrapper.GrpcHost;

static class Extensions
{
    public static Task DelegateConfigureServicesAsync(this IHostApplicationBuilder builder)
    {
        return RegisterServicesActivity
            .FromBuilder(builder)
            .ConfigureBuilder(builder, CancellationToken.None);
    }

    public static async Task DelegateConfigureRoutesAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        foreach (var activity in scope.ServiceProvider.GetServices<IActivity>())
            await activity.ConfigureApplication(app, CancellationToken.None);
    }

    public static bool IsMasterGrpcServer(this WebApplication app)
        => string.IsNullOrWhiteSpace(app.Configuration.GetValue<string>("Discovery:Master"));
}

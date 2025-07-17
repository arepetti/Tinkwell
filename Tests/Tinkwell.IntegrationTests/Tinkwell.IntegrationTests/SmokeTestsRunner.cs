namespace Tinkwell.IntegrationTests;

public class SmokeTestsRunner : IClassFixture<SupervisorFixture>
{
    public SmokeTestsRunner(SupervisorFixture fixture)
    {
        _fixture = fixture;

        // We're going to ask the supervisor to work in a different working directory.
        // The SupervisorFixture class already created this temporary directory where we can dump
        // our entire configuration and it'll be deleted at the end.
        // TODO: scripting AND...copy from FS, not from a string...
        var ensambleContent = "compose service orchestrator \"Tinkwell.Orchestrator.dll\"\ncompose service store \"Tinkwell.Store.dll\"";
        var ensamblePath = Path.Combine(_fixture.SupervisorWorkingDirectory, "ensamble.tw");
        File.WriteAllText(ensamblePath, ensambleContent);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public Task Run()
    {
        Assert.NotNull(_fixture.ClientCertificate);
        Assert.True(_fixture.ClientCertificate.HasPrivateKey);

        // TODO: here we should call/parse/whatever the script that does our
        // integration tests. A list of processes to run?
        return Task.CompletedTask;
    }

    private readonly SupervisorFixture _fixture;
}
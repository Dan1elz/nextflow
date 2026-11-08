using System.Net;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Nextflow.Infrastructure.Services;

namespace Nextflow.Tests.Unit.Forecast;

/// <summary>
/// Cliente do Gemini com um HttpMessageHandler fake (nenhum teste chama a IA real): sobrecarga e tempo limite no modelo principal
/// caem para o modelo reserva antes de o lote ir para o cálculo.
/// </summary>
public class GeminiAiClientTests
{
    private const string Primary = "modelo-principal";
    private const string Fallback = "modelo-reserva";

    private static string Success(string text)
        => $$"""{"candidates":[{"content":{"parts":[{"text":{{System.Text.Json.JsonSerializer.Serialize(text)}}}]},"finishReason":"STOP"}]}""";

    private const string Overloaded = """{"error":{"code":503,"message":"This model is currently experiencing high demand."}}""";

    private static GeminiAiClient Client(FakeHandler handler, string? fallback = Fallback, int timeoutSeconds = 60)
    {
        var settings = new Dictionary<string, string?>
        {
            ["Ai:ApiKey"] = "chave-de-teste",
            ["Ai:Model"] = Primary,
            ["Ai:FallbackModel"] = fallback,
            ["Ai:TimeoutSeconds"] = timeoutSeconds.ToString()
        };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        return new GeminiAiClient(new HttpClient(handler), configuration, NullLogger<GeminiAiClient>.Instance, TimeSpan.Zero);
    }

    private static Task<string> Generate(GeminiAiClient client)
        => client.GenerateJsonAsync("sistema", "usuario", new { type = "OBJECT" }, CancellationToken.None);

    [Fact]
    public async Task Should_UsePrimaryModel_When_ItAnswers()
    {
        var handler = new FakeHandler(_ => (HttpStatusCode.OK, Success("{\"ok\":true}")));
        var client = Client(handler);

        var json = await Generate(client);

        json.ShouldBe("{\"ok\":true}");
        handler.Models.ShouldBe([Primary]);
        client.Model.ShouldBe(Primary);
    }

    [Fact]
    public async Task Should_FallBackToReserveModel_When_PrimaryIsOverloaded()
    {
        var handler = new FakeHandler(model => model == Primary
            ? (HttpStatusCode.ServiceUnavailable, Overloaded)
            : (HttpStatusCode.OK, Success("{\"ok\":true}")));
        var client = Client(handler);

        var json = await Generate(client);

        json.ShouldBe("{\"ok\":true}");
        handler.Models.ShouldBe([Primary, Primary, Fallback]);
        client.Model.ShouldBe(Fallback); // a análise registra o modelo que de fato respondeu
    }

    [Fact]
    public async Task Should_FallBackToReserveModel_When_PrimaryTimesOut()
    {
        var handler = new FakeHandler(model => model == Primary
            ? (HttpStatusCode.OK, Success("tarde demais"))
            : (HttpStatusCode.OK, Success("{\"ok\":true}")))
        { HangFor = Primary };
        var client = Client(handler, timeoutSeconds: 1);

        var json = await Generate(client);

        json.ShouldBe("{\"ok\":true}");
        handler.Models.ShouldBe([Primary, Fallback]); // travou uma vez: não insiste no principal
    }

    [Fact]
    public async Task Should_Throw_When_BothModelsAreOverloaded()
    {
        var handler = new FakeHandler(_ => (HttpStatusCode.ServiceUnavailable, Overloaded));

        var error = await Should.ThrowAsync<HttpRequestException>(() => Generate(Client(handler)));

        error.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        error.Message.ShouldContain(Fallback);
        handler.Models.ShouldBe([Primary, Primary, Fallback, Fallback]);
    }

    [Fact]
    public async Task Should_NotTryReserveModel_When_RequestIsRejected()
    {
        var handler = new FakeHandler(_ => (HttpStatusCode.BadRequest, """{"error":{"message":"API key not valid."}}"""));

        await Should.ThrowAsync<HttpRequestException>(() => Generate(Client(handler)));

        handler.Models.ShouldBe([Primary]);
    }

    [Fact]
    public async Task Should_TryOnlyPrimary_When_FallbackIsTheSameModel()
    {
        var handler = new FakeHandler(_ => (HttpStatusCode.ServiceUnavailable, Overloaded));

        await Should.ThrowAsync<HttpRequestException>(() => Generate(Client(handler, fallback: Primary)));

        handler.Models.ShouldBe([Primary, Primary]);
    }

    [Fact]
    public async Task Should_SendKeyOnlyInHeader_When_CallingTheApi()
    {
        var handler = new FakeHandler(_ => (HttpStatusCode.OK, Success("{}")));

        await Generate(Client(handler));

        handler.Requests.ShouldAllBe(r => !r.Url.Contains("chave-de-teste") && r.Key == "chave-de-teste");
    }

    private sealed class FakeHandler(Func<string, (HttpStatusCode Status, string Body)> respond) : HttpMessageHandler
    {
        public string? HangFor { get; init; }
        public List<string> Models { get; } = [];
        public List<(string Url, string? Key)> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var url = request.RequestUri!.ToString();
            var model = Uri.UnescapeDataString(url.Split("/models/")[1].Split(':')[0]);
            Models.Add(model);
            Requests.Add((url, request.Headers.TryGetValues("x-goog-api-key", out var keys) ? keys.Single() : null));

            if (model == HangFor)
                await Task.Delay(Timeout.Infinite, cancellationToken);

            var (status, body) = respond(model);
            return new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        }
    }
}

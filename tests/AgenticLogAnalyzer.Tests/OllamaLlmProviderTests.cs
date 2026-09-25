using System.Net;
using System.Text;
using System.Text.Json;
using AgenticLogAnalyzer.Llm;

namespace AgenticLogAnalyzer.Tests;

public sealed class OllamaLlmProviderTests
{
    private static readonly OllamaOptions Options = new(new Uri("http://ollama.test:11434"), "llama3.2", "10m");

    [Fact]
    public async Task GenerateAsync_PostsNonStreamingChatRequest_AndReturnsMessageContent()
    {
        var handler = new StubHandler(_ => Json("""{"message":{"role":"assistant","content":"Réponse"}}"""));
        var provider = new OllamaLlmProvider(new HttpClient(handler), Options);

        var answer = await provider.GenerateAsync("system prompt", "user prompt", CancellationToken.None);

        Assert.Equal("Réponse", answer);
        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("http://ollama.test:11434/api/chat", request.Uri.ToString());
        using var body = JsonDocument.Parse(request.Body);
        Assert.Equal("llama3.2", body.RootElement.GetProperty("model").GetString());
        Assert.False(body.RootElement.GetProperty("stream").GetBoolean());
        Assert.Equal("10m", body.RootElement.GetProperty("keep_alive").GetString());
        Assert.Equal(350, body.RootElement.GetProperty("options").GetProperty("num_predict").GetInt32());
        var messages = body.RootElement.GetProperty("messages");
        Assert.Equal("system", messages[0].GetProperty("role").GetString());
        Assert.Equal("user prompt", messages[1].GetProperty("content").GetString());
    }

    [Fact]
    public async Task GenerateAsync_HttpError_ThrowsInvalidOperationException()
    {
        var provider = new OllamaLlmProvider(
            new HttpClient(new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new StringContent("model not found") })),
            Options);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => provider.GenerateAsync("system", "prompt", CancellationToken.None));

        Assert.Contains("404", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("llama3.2:latest", true)]
    [InlineData("llama3.2", true)]
    [InlineData("qwen2.5:3b", false)]
    public async Task GetStatusAsync_ReportsWhetherConfiguredModelIsInstalled(string installed, bool expected)
    {
        var provider = new OllamaLlmProvider(
            new HttpClient(new StubHandler(_ => Json($$"""{"models":[{"name":"{{installed}}"}]}"""))),
            Options);

        var status = await provider.GetStatusAsync(CancellationToken.None);

        Assert.True(status.Reachable);
        Assert.Equal(expected, status.ModelAvailable);
        Assert.Equal(expected, status.Error is null);
    }

    [Fact]
    public async Task GetStatusAsync_UnreachableServer_ReturnsUnavailableInsteadOfThrowing()
    {
        var provider = new OllamaLlmProvider(
            new HttpClient(new StubHandler(_ => throw new HttpRequestException("Connection refused"))),
            Options);

        var status = await provider.GetStatusAsync(CancellationToken.None);

        Assert.False(status.Reachable);
        Assert.False(status.ModelAvailable);
        Assert.Contains("Connection refused", status.Error, StringComparison.Ordinal);
    }

    private static HttpResponseMessage Json(string json) =>
        new(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<(HttpMethod Method, Uri Uri, string Body)> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
            Requests.Add((request.Method, request.RequestUri!, body));
            return respond(request);
        }
    }
}

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using VidroApi.IntegrationTests.Common;

namespace VidroApi.IntegrationTests.Channels;

public class GetChannelTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerOptions.Default)
    {
        PropertyNameCaseInsensitive = true
    };

    [Fact]
    public async Task GetChannel_WithValidId_Returns200WithChannelData()
    {
        var (accessToken, username) = await SignUpAndGetCredentials();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var createResponse = await _client.PostAsJsonAsync("/v1/channels", new { name = "My Channel", description = "A description" });
        var createBody = await createResponse.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var channelId = createBody.GetProperty("data").GetProperty("channelId").GetString();

        _client.DefaultRequestHeaders.Authorization = null;
        var response = await _client.GetAsync($"/v1/channels/{channelId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var data = body.GetProperty("data");
        data.GetProperty("channelId").GetString().Should().Be(channelId);
        data.GetProperty("name").GetString().Should().Be("My Channel");
        data.GetProperty("description").GetString().Should().Be("A description");
        data.GetProperty("followerCount").GetInt32().Should().Be(0);
        data.GetProperty("ownerUsername").GetString().Should().Be(username);
        data.GetProperty("avatarUrl").ValueKind.Should().Be(JsonValueKind.Null);
        data.GetProperty("ownerAvatarUrl").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task GetChannel_WithNonExistentId_Returns404()
    {
        var response = await _client.GetAsync($"/v1/channels/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetChannel_IsPublic_NoAuthRequired()
    {
        var (accessToken, _) = await SignUpAndGetCredentials();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var createResponse = await _client.PostAsJsonAsync("/v1/channels", new { name = "Public Channel" });
        var createBody = await createResponse.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var channelId = createBody.GetProperty("data").GetProperty("channelId").GetString();

        _client.DefaultRequestHeaders.Authorization = null;
        var response = await _client.GetAsync($"/v1/channels/{channelId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private async Task<(string AccessToken, string Username)> SignUpAndGetCredentials()
    {
        var username = $"usr{Guid.NewGuid():N}"[..15];
        var email = $"user_{Guid.NewGuid():N}@example.com";
        var password = "StrongPass1!";

        await _client.PostAsJsonAsync("/v1/auth/signup", new { username, email, password });

        var signInResponse = await _client.PostAsJsonAsync("/v1/auth/signin", new { email, password });
        var body = await signInResponse.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var accessToken = body.GetProperty("data").GetProperty("accessToken").GetString()!;

        return (accessToken, username);
    }
}

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using VidroApi.IntegrationTests.Common;

namespace VidroApi.IntegrationTests.Channels;

public class UploadChannelAvatarTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerOptions.Default)
    {
        PropertyNameCaseInsensitive = true
    };

    [Fact]
    public async Task UploadChannelAvatar_WhenOwner_Returns200WithUploadUrl()
    {
        var (accessToken, channelId) = await CreateChannelAndGetIds();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await _client.PostAsync($"/v1/channels/{channelId}/avatar", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var data = body.GetProperty("data");
        data.GetProperty("uploadUrl").GetString().Should().NotBeNullOrWhiteSpace();
        data.GetProperty("uploadExpiresAt").GetString().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task UploadChannelAvatar_WhenNotOwner_Returns403()
    {
        var (ownerToken, channelId) = await CreateChannelAndGetIds();
        var nonOwnerToken = await SignUpAndGetAccessToken();

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", nonOwnerToken);
        var response = await _client.PostAsync($"/v1/channels/{channelId}/avatar", null);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UploadChannelAvatar_WithNonExistentChannel_Returns404()
    {
        var accessToken = await SignUpAndGetAccessToken();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await _client.PostAsync($"/v1/channels/{Guid.NewGuid()}/avatar", null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UploadChannelAvatar_WhenNotAuthenticated_Returns401()
    {
        var response = await _client.PostAsync($"/v1/channels/{Guid.NewGuid()}/avatar", null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetChannel_ReturnsAvatarUrl_AfterUpload()
    {
        var (accessToken, channelId) = await CreateChannelAndGetIds();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        await _client.PostAsync($"/v1/channels/{channelId}/avatar", null);

        _client.DefaultRequestHeaders.Authorization = null;
        var getResponse = await _client.GetAsync($"/v1/channels/{channelId}");
        var body = await getResponse.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var avatarUrl = body.GetProperty("data").GetProperty("avatarUrl").GetString();

        avatarUrl.Should().NotBeNullOrWhiteSpace();
    }

    private async Task<(string AccessToken, Guid ChannelId)> CreateChannelAndGetIds()
    {
        var accessToken = await SignUpAndGetAccessToken();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var createResponse = await _client.PostAsJsonAsync("/v1/channels", new { name = $"Channel {Guid.NewGuid()}" });
        var body = await createResponse.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var channelId = Guid.Parse(body.GetProperty("data").GetProperty("channelId").GetString()!);

        return (accessToken, channelId);
    }

    private async Task<string> SignUpAndGetAccessToken()
    {
        var email = $"user_{Guid.NewGuid():N}@example.com";
        var password = "StrongPass1!";

        await _client.PostAsJsonAsync("/v1/auth/signup", new
        {
            username = $"usr{Guid.NewGuid():N}"[..15],
            email,
            password
        });

        var signInResponse = await _client.PostAsJsonAsync("/v1/auth/signin", new { email, password });
        var body = await signInResponse.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        return body.GetProperty("data").GetProperty("accessToken").GetString()!;
    }
}

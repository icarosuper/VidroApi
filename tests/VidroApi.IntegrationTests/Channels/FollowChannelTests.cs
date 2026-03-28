using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using VidroApi.IntegrationTests.Common;

namespace VidroApi.IntegrationTests.Channels;

public class FollowChannelTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerOptions.Default)
    {
        PropertyNameCaseInsensitive = true
    };

    [Fact]
    public async Task FollowChannel_WithValidChannel_Returns204()
    {
        var channelId = await CreateChannelAsNewUser();
        var followerToken = await SignUpAndGetAccessToken();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", followerToken);

        var response = await _client.PostAsync($"/v1/channels/{channelId}/follow", null);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task FollowChannel_IncrementsFollowerCount()
    {
        var channelId = await CreateChannelAsNewUser();
        var followerToken = await SignUpAndGetAccessToken();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", followerToken);

        await _client.PostAsync($"/v1/channels/{channelId}/follow", null);

        _client.DefaultRequestHeaders.Authorization = null;
        var getResponse = await _client.GetAsync($"/v1/channels/{channelId}");
        var body = await getResponse.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);

        body.GetProperty("data").GetProperty("followerCount").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task FollowChannel_WhenAlreadyFollowing_Returns409WithExpectedCode()
    {
        var channelId = await CreateChannelAsNewUser();
        var followerToken = await SignUpAndGetAccessToken();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", followerToken);

        await _client.PostAsync($"/v1/channels/{channelId}/follow", null);
        var response = await _client.PostAsync($"/v1/channels/{channelId}/follow", null);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        body.GetProperty("code").GetString().Should().Be("channel.already_following");
    }

    [Fact]
    public async Task FollowChannel_WithoutAuth_Returns401()
    {
        var channelId = await CreateChannelAsNewUser();

        var response = await _client.PostAsync($"/v1/channels/{channelId}/follow", null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task FollowChannel_OwnChannel_Returns409WithExpectedCode()
    {
        var accessToken = await SignUpAndGetAccessToken();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var createResponse = await _client.PostAsJsonAsync("/v1/channels", new { name = "My Channel" });
        var createBody = await createResponse.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var channelId = createBody.GetProperty("data").GetProperty("channelId").GetString();

        var response = await _client.PostAsync($"/v1/channels/{channelId}/follow", null);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        body.GetProperty("code").GetString().Should().Be("channel.cannot_follow_own");
    }

    [Fact]
    public async Task FollowChannel_WithNonExistentId_Returns404()
    {
        var followerToken = await SignUpAndGetAccessToken();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", followerToken);

        var response = await _client.PostAsync($"/v1/channels/{Guid.NewGuid()}/follow", null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
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

    private async Task<string> CreateChannelAsNewUser()
    {
        var accessToken = await SignUpAndGetAccessToken();
        var previousAuth = _client.DefaultRequestHeaders.Authorization;
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await _client.PostAsJsonAsync("/v1/channels", new { name = "A Channel" });
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var channelId = body.GetProperty("data").GetProperty("channelId").GetString()!;

        _client.DefaultRequestHeaders.Authorization = previousAuth;
        return channelId;
    }
}

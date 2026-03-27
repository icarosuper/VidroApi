using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using VidroApi.IntegrationTests.Common;

namespace VidroApi.IntegrationTests.Channels;

public class UpdateChannelTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerOptions.Default)
    {
        PropertyNameCaseInsensitive = true
    };

    [Fact]
    public async Task UpdateChannel_WithValidData_Returns204()
    {
        var accessToken = await SignUpAndGetAccessToken();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var channelId = await CreateChannel("Original Name");

        var response = await _client.PutAsJsonAsync($"/v1/channels/{channelId}", new
        {
            name = "Updated Name",
            description = "Updated description"
        });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task UpdateChannel_ChangesArePersisted()
    {
        var accessToken = await SignUpAndGetAccessToken();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var channelId = await CreateChannel("Original Name");

        await _client.PutAsJsonAsync($"/v1/channels/{channelId}", new
        {
            name = "Updated Name",
            description = "Updated description"
        });

        _client.DefaultRequestHeaders.Authorization = null;
        var getResponse = await _client.GetAsync($"/v1/channels/{channelId}");
        var body = await getResponse.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var data = body.GetProperty("data");

        data.GetProperty("name").GetString().Should().Be("Updated Name");
        data.GetProperty("description").GetString().Should().Be("Updated description");
    }

    [Fact]
    public async Task UpdateChannel_WithoutAuth_Returns401()
    {
        var accessToken = await SignUpAndGetAccessToken();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var channelId = await CreateChannel("My Channel");

        _client.DefaultRequestHeaders.Authorization = null;
        var response = await _client.PutAsJsonAsync($"/v1/channels/{channelId}", new { name = "Updated" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UpdateChannel_WhenNotOwner_Returns403WithExpectedCode()
    {
        var ownerToken = await SignUpAndGetAccessToken();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);
        var channelId = await CreateChannel("Owner Channel");

        var otherToken = await SignUpAndGetAccessToken();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", otherToken);
        var response = await _client.PutAsJsonAsync($"/v1/channels/{channelId}", new { name = "Hijacked" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        body.GetProperty("code").GetString().Should().Be("channel.not_owner");
    }

    [Fact]
    public async Task UpdateChannel_WithNonExistentId_Returns404()
    {
        var accessToken = await SignUpAndGetAccessToken();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await _client.PutAsJsonAsync($"/v1/channels/{Guid.NewGuid()}", new { name = "Updated" });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UpdateChannel_WithEmptyName_Returns400()
    {
        var accessToken = await SignUpAndGetAccessToken();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var channelId = await CreateChannel("My Channel");

        var response = await _client.PutAsJsonAsync($"/v1/channels/{channelId}", new { name = "" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UpdateChannel_WithNameTooLong_Returns400()
    {
        var accessToken = await SignUpAndGetAccessToken();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var channelId = await CreateChannel("My Channel");

        var response = await _client.PutAsJsonAsync($"/v1/channels/{channelId}", new { name = new string('a', 101) });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
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

    private async Task<string> CreateChannel(string name)
    {
        var response = await _client.PostAsJsonAsync("/v1/channels", new { name });
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        return body.GetProperty("data").GetProperty("channelId").GetString()!;
    }
}

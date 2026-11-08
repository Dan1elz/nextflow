using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Nextflow.Domain.Dtos.Notifications;
using Nextflow.Domain.Enums;
using Nextflow.Domain.Models;

namespace Nextflow.Tests.Integration;

/// <summary>Sino de notificações (T-DF12, F3.5) pela API: contador, lista do usuário, marcar uma e todas como lidas.</summary>
[Collection(AccessApiCollection.Name)]
public class NotificationsApiTests(AccessApiFixture api)
{
    private const string Route = "/api/notifications";

    private static async Task<JsonElement> Json(HttpResponseMessage response)
        => await response.Content.ReadFromJsonAsync<JsonElement>();

    private async Task<List<Guid>> Seed(Guid userId, int count)
    {
        await using var context = api.CreateContext();
        var rows = Enumerable.Range(1, count)
            .Select(i => new Notification(new NewNotificationDto(NotificationTypeEnum.LowStock, $"Aviso {i}", "Teste.", "/products"), userId))
            .ToList();
        context.AddRange(rows);
        await context.SaveChangesAsync();
        return [.. rows.Select(r => r.Id)];
    }

    [Fact]
    [Trait("Rule", "L-DF8")]
    public async Task Should_ListAndMarkRead_When_UserHasNotifications()
    {
        var user = await api.CreateUserAsync();
        var token = (await api.LoginAsync(user.Email, user.Password)).GetProperty("token").GetString()!;
        var client = api.Authenticated(token);
        var ids = await Seed(user.Id, 3);

        var list = await Json(await client.GetAsync(Route));
        list.GetProperty("unreadCount").GetInt32().ShouldBe(3);
        list.GetProperty("items").GetArrayLength().ShouldBe(3);

        (await client.PatchAsync($"{Route}/{ids[0]}/read", null)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var unread = await Json(await client.GetAsync($"{Route}?unreadOnly=true"));
        unread.GetProperty("unreadCount").GetInt32().ShouldBe(2);
        unread.GetProperty("items").EnumerateArray().Select(n => n.GetProperty("id").GetGuid()).ShouldNotContain(ids[0]);

        (await client.PatchAsync($"{Route}/read-all", null)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await Json(await client.GetAsync(Route))).GetProperty("unreadCount").GetInt32().ShouldBe(0);

        await using var context = api.CreateContext();
        (await context.Set<Notification>().Where(n => n.UserId == user.Id).ToListAsync()).ShouldAllBe(n => n.ReadAt != null);
    }

    [Fact]
    [Trait("Rule", "L-DF8")]
    public async Task Should_Return404_When_NotificationIsFromAnotherUser()
    {
        var owner = await api.CreateUserAsync();
        var other = await api.CreateUserAsync();
        var ids = await Seed(owner.Id, 1);
        var token = (await api.LoginAsync(other.Email, other.Password)).GetProperty("token").GetString()!;
        var client = api.Authenticated(token);

        (await client.PatchAsync($"{Route}/{ids[0]}/read", null)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await Json(await client.GetAsync(Route))).GetProperty("items").EnumerateArray()
            .Select(n => n.GetProperty("id").GetGuid()).ShouldNotContain(ids[0]);
    }
}

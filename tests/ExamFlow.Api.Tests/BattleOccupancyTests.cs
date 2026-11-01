using ExamFlow.Api.Features.Battles;

namespace ExamFlow.Api.Tests;

public class BattleOccupancyTests
{
    [Fact]
    public void Host_keeps_ownership_while_another_tab_is_still_connected()
    {
        var room = Room();
        var host = new BattleParticipant(room.HostUserId, "Host", DateTimeOffset.UtcNow);
        room.Participants[host.UserId] = host;
        host.Connections.UnionWith(["tab-1", "tab-2"]);
        host.Connections.Remove("tab-1");

        Assert.Equal(host.UserId, room.ConnectedHostId);
    }

    [Fact]
    public void Remaining_connected_participant_succeeds_a_disconnected_host()
    {
        var room = Room();
        room.Participants[room.HostUserId] = new BattleParticipant(room.HostUserId, "Host", DateTimeOffset.UtcNow);
        var remaining = new BattleParticipant(Guid.NewGuid(), "Remaining", DateTimeOffset.UtcNow);
        remaining.Connections.Add("remaining-tab");
        room.Participants[remaining.UserId] = remaining;

        Assert.Equal(remaining.UserId, room.ConnectedHostId);
    }

    [Fact]
    public void Offline_members_do_not_prevent_a_room_from_being_empty()
    {
        var room = Room();
        room.Participants[room.HostUserId] = new BattleParticipant(room.HostUserId, "Host", DateTimeOffset.UtcNow);

        Assert.Null(room.ConnectedHostId);
    }

    private static BattleRoom Room() => new("ABC123", Guid.NewGuid(),
        new BattleSettings(Guid.NewGuid(), "Sample", 1, 10, 2, 20), DateTimeOffset.UtcNow);
}

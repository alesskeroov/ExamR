using ExamFlow.Api.Features.Friends;

namespace ExamFlow.Api.Tests;

public class SocialPresenceTests
{
    [Fact]
    public void Closing_one_tab_keeps_the_user_online_until_the_last_connection_closes()
    {
        var presence = new SocialPresence();
        var user = Guid.NewGuid();
        presence.Add(user, "tab-1");
        presence.Add(user, "tab-2");

        presence.Remove(user, "tab-1");
        presence.Remove(user, "unknown");
        Assert.True(presence.IsOnline(user));

        presence.Remove(user, "tab-2");
        Assert.False(presence.IsOnline(user));
    }

    [Fact]
    public void Disconnecting_one_user_does_not_change_another_users_presence()
    {
        var presence = new SocialPresence();
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        presence.Add(first, "first");
        presence.Add(second, "second");

        presence.Remove(first, "first");

        Assert.False(presence.IsOnline(first));
        Assert.True(presence.IsOnline(second));
    }
}

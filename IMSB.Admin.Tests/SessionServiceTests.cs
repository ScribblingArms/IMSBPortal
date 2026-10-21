using IMSB.Dto.Models;
using IMSB.Admin.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace IMSB.Admin.Tests;

/// <summary>A clock the tests move by hand.</summary>
public sealed class ManualClock : TimeProvider
{
    public DateTimeOffset Now { get; set; } = new(2026, 10, 8, 9, 0, 0, TimeSpan.Zero);
    public override DateTimeOffset GetUtcNow() => Now;
    public void Advance(TimeSpan by) => Now += by;
}

public class SessionServiceTests
{
    private static (IMSBContext Ctx, SessionService Sessions, ManualClock Clock, Login Login) Setup(UserSessionOptions? options = null)
    {
        var ctx = TestHelpers.CreateContext(seedReferenceData: false);
        var login = new Login { Username = "admin@imsb", PasswordHash = "x", Role = "Admin" };
        ctx.Logins.Add(login);
        ctx.SaveChanges();
        var clock = new ManualClock();
        return (ctx, new SessionService(ctx, Options.Create(options ?? new UserSessionOptions()), clock), clock, login);
    }

    [Fact]
    public async Task Start_records_the_session()
    {
        var (ctx, sessions, clock, login) = Setup();
        var s = await sessions.StartAsync(login, "Admin", rememberMe: false, "10.0.0.1", "Mozilla/5.0 Chrome/120");
        var row = ctx.UserSessions.AsNoTracking().Single();
        Assert.Equal((login.Id, "Admin", clock.Now.UtcDateTime, clock.Now.UtcDateTime.AddHours(8), false, "10.0.0.1"),
            (row.LoginId, row.Role, row.CreatedAt, row.ExpiresAt, row.IsPersistent, row.IpAddress));
        Assert.Equal((SessionCheck.Valid, s.Id), ((await sessions.ValidateAsync(s.Id)).Result, s.Id));
    }

    [Fact]
    public async Task Idle_session_times_out_and_is_closed()
    {
        var (ctx, sessions, clock, login) = Setup();
        var s = await sessions.StartAsync(login, "Admin", false, null, null);
        clock.Advance(TimeSpan.FromMinutes(29));
        Assert.Equal(SessionCheck.Valid, (await sessions.ValidateAsync(s.Id)).Result);   // activity resets the idle clock

        clock.Advance(TimeSpan.FromMinutes(29));
        Assert.Equal(SessionCheck.Valid, (await sessions.ValidateAsync(s.Id)).Result);

        clock.Advance(TimeSpan.FromMinutes(30));
        Assert.Equal(SessionCheck.IdleTimeout, (await sessions.ValidateAsync(s.Id)).Result);
        Assert.Equal(SessionEndReasons.IdleTimeout, ctx.UserSessions.AsNoTracking().Single().RevokedReason);
        // once closed it stays closed
        Assert.Equal(SessionCheck.Revoked, (await sessions.ValidateAsync(s.Id)).Result);
    }

    [Fact]
    public async Task Activity_cannot_extend_past_the_absolute_limit()
    {
        var (_, sessions, clock, login) = Setup(new UserSessionOptions { AbsoluteTimeoutHours = 1, IdleTimeoutMinutes = 30 });
        var s = await sessions.StartAsync(login, "Admin", false, null, null);
        for (int i = 0; i < 3; i++) { clock.Advance(TimeSpan.FromMinutes(19)); Assert.Equal(SessionCheck.Valid, (await sessions.ValidateAsync(s.Id)).Result); }
        clock.Advance(TimeSpan.FromMinutes(3));   // 60 min: active the whole time, but the hour is up
        Assert.Equal(SessionCheck.Expired, (await sessions.ValidateAsync(s.Id)).Result);
    }

    [Fact]
    public async Task Remember_me_ignores_idle_but_not_its_lifetime()
    {
        var (_, sessions, clock, login) = Setup();
        var s = await sessions.StartAsync(login, "Admin", rememberMe: true, null, null);
        clock.Advance(TimeSpan.FromDays(3));
        Assert.Equal(SessionCheck.Valid, (await sessions.ValidateAsync(s.Id)).Result);
        clock.Advance(TimeSpan.FromDays(5));
        Assert.Equal(SessionCheck.Expired, (await sessions.ValidateAsync(s.Id)).Result);
    }

    [Fact]
    public async Task Disabled_account_ends_its_sessions()
    {
        var (ctx, sessions, _, login) = Setup();
        var s = await sessions.StartAsync(login, "Admin", false, null, null);
        login.IsActive = false;
        ctx.SaveChanges();
        Assert.Equal(SessionCheck.AccountDisabled, (await sessions.ValidateAsync(s.Id)).Result);
        Assert.Equal(SessionEndReasons.AccountDisabled, ctx.UserSessions.AsNoTracking().Single().RevokedReason);
    }

    [Fact]
    public async Task Unknown_session_is_not_found()
    {
        var (_, sessions, _, _) = Setup();
        Assert.Equal(SessionCheck.NotFound, (await sessions.ValidateAsync(Guid.NewGuid())).Result);
    }

    [Fact]
    public async Task Revoke_one_and_revoke_all_but_current()
    {
        var (_, sessions, _, login) = Setup();
        var a = await sessions.StartAsync(login, "Admin", false, null, null);
        var b = await sessions.StartAsync(login, "Admin", false, null, null);
        var c = await sessions.StartAsync(login, "Admin", false, null, null);

        Assert.True(await sessions.RevokeAsync(a.Id, SessionEndReasons.SignedOut));
        Assert.False(await sessions.RevokeAsync(a.Id, SessionEndReasons.SignedOut));   // already ended
        Assert.Equal(1, await sessions.RevokeAllAsync(login.Id, SessionEndReasons.Revoked, except: c.Id));

        Assert.Equal(SessionCheck.Revoked, (await sessions.ValidateAsync(a.Id)).Result);
        Assert.Equal(SessionCheck.Revoked, (await sessions.ValidateAsync(b.Id)).Result);
        Assert.Equal(SessionCheck.Valid, (await sessions.ValidateAsync(c.Id)).Result);
        Assert.Equal(new[] { c.Id }, sessions.Active().Select(s => s.Id).ToList());
    }

    [Fact]
    public async Task Last_seen_is_written_at_most_once_a_minute()
    {
        var (ctx, sessions, clock, login) = Setup();
        var s = await sessions.StartAsync(login, "Admin", false, null, null);
        var start = clock.Now.UtcDateTime;
        clock.Advance(TimeSpan.FromSeconds(30));
        await sessions.ValidateAsync(s.Id);
        Assert.Equal(start, ctx.UserSessions.AsNoTracking().Single().LastSeenAt);
        clock.Advance(TimeSpan.FromSeconds(40));
        await sessions.ValidateAsync(s.Id);
        Assert.Equal(clock.Now.UtcDateTime, ctx.UserSessions.AsNoTracking().Single().LastSeenAt);
    }

    [Fact]
    public async Task Seconds_left_is_the_earlier_of_idle_and_absolute()
    {
        var (_, sessions, clock, login) = Setup(new UserSessionOptions { IdleTimeoutMinutes = 30, AbsoluteTimeoutHours = 1 });
        var s = await sessions.StartAsync(login, "Admin", false, null, null);
        Assert.Equal(30 * 60, sessions.SecondsLeft(s));
        clock.Advance(TimeSpan.FromMinutes(45));
        s.LastSeenAt = clock.Now.UtcDateTime;
        Assert.Equal(15 * 60, sessions.SecondsLeft(s));
    }

    [Fact]
    public async Task Old_ended_sessions_are_pruned_at_the_next_sign_in()
    {
        var (ctx, sessions, clock, login) = Setup();
        var old = await sessions.StartAsync(login, "Admin", false, null, null);
        await sessions.RevokeAsync(old.Id, SessionEndReasons.SignedOut);
        clock.Advance(TimeSpan.FromDays(31));
        await sessions.StartAsync(login, "Admin", false, null, null);
        Assert.DoesNotContain(ctx.UserSessions.AsNoTracking(), s => s.Id == old.Id);
    }

    [Theory]
    [InlineData("GET", "text/html,application/xhtml+xml", null, false)]
    [InlineData("GET", "*/*", null, true)]
    [InlineData("GET", "application/json", null, true)]
    [InlineData("POST", "*/*", "application/json", true)]
    [InlineData("POST", "text/html", "application/x-www-form-urlencoded", false)]
    public void Script_requests_are_told_apart_from_navigations(string method, string accept, string? contentType, bool script)
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Method = method;
        ctx.Request.Headers.Accept = accept;
        if (contentType != null) ctx.Request.ContentType = contentType;
        Assert.Equal(script, SessionCookieEvents.IsScriptRequest(ctx.Request));
    }
}

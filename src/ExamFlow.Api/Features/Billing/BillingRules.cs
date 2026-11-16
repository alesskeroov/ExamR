using System.Globalization;

namespace ExamFlow.Api.Features.Billing;

public enum PlanChangeKind { New, Extend, Upgrade, Downgrade }

/// <summary>A period the user already has (active or scheduled).</summary>
public sealed record PeriodItem(Guid PlanId, decimal Price, int DurationDays, DateTimeOffset StartsAt, DateTimeOffset EndsAt);
public sealed record PlanTarget(Guid PlanId, decimal Price, int DurationDays);
public sealed record PlanChange(PlanChangeKind Kind, DateTimeOffset StartsAt, DateTimeOffset EndsAt, int BonusDays);

/// <summary>Pure package rules (no DB) so they can be unit-tested.</summary>
public static class BillingRules
{
    public const int MaxPeriods = 12;
    private const string CodeAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    /// <summary>
    /// What buying <paramref name="target"/> does to the user's current periods:
    /// none → starts now; same package as the last period → appended; cheaper → starts when the current periods end;
    /// otherwise (upgrade) → starts now and the unused value of the current periods becomes bonus days.
    /// </summary>
    public static PlanChange Decide(IReadOnlyList<PeriodItem> chain, PlanTarget target, int periods, int extraDays, DateTimeOffset now)
    {
        var live = chain.Where(x => x.EndsAt > now).OrderBy(x => x.StartsAt).ToList();
        var length = TimeSpan.FromDays(target.DurationDays * periods + extraDays);
        if (live.Count == 0) return new(PlanChangeKind.New, now, now + length, 0);
        var last = live[^1];
        if (last.PlanId == target.PlanId) return new(PlanChangeKind.Extend, last.EndsAt, last.EndsAt + length, 0);
        if (target.Price < last.Price) return new(PlanChangeKind.Downgrade, last.EndsAt, last.EndsAt + length, 0);
        var bonus = BonusDays(live, target, now);
        return new(PlanChangeKind.Upgrade, now, now + length + TimeSpan.FromDays(bonus), bonus);
    }

    /// <summary>Unused value of the periods (price per day × remaining days) converted to days of the target package, rounded down.</summary>
    public static int BonusDays(IEnumerable<PeriodItem> live, PlanTarget target, DateTimeOffset now)
    {
        if (target.Price <= 0 || target.DurationDays <= 0) return 0;
        var value = live.Where(x => x.EndsAt > now && x.DurationDays > 0).Sum(x =>
        {
            var from = x.StartsAt > now ? x.StartsAt : now;
            return (decimal)(x.EndsAt - from).TotalDays * x.Price / x.DurationDays;
        });
        // The tiny epsilon keeps exact whole days (e.g. 21.9999… from repeating decimals) from losing a day.
        return (int)Math.Floor(value * target.DurationDays / target.Price + 0.000001m);
    }

    public static string NewCode(Random random) =>
        "EXR-" + new string(Enumerable.Range(0, 4).Select(_ => CodeAlphabet[random.Next(CodeAlphabet.Length)]).ToArray());

    public static string Money(decimal amount) => amount.ToString("0.00", CultureInfo.InvariantCulture);

    public static string RequestMessage(string planName, string code, int days, decimal amount) =>
        $"Salam! «{planName}» paketini almaq istəyirəm. Kod: {code}, müddət: {days} gün, məbləğ: {Money(amount)} AZN.";

    public static string? WhatsAppLink(string? number, string? text) =>
        string.IsNullOrWhiteSpace(number) ? null
            : text is null ? $"https://wa.me/{number}" : $"https://wa.me/{number}?text={Uri.EscapeDataString(text)}";

    public static string? InstagramLink(string? username) =>
        string.IsNullOrWhiteSpace(username) ? null : $"https://ig.me/m/{username}";
}

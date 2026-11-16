using ExamFlow.Api.Features.Billing;

namespace ExamFlow.Api.Tests;

public class BillingRulesTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-11-16T12:00:00Z");
    private static readonly Guid Week = Guid.NewGuid(), Standart = Guid.NewGuid(), Pro = Guid.NewGuid(), Premium = Guid.NewGuid();
    private static readonly PlanTarget WeekPlan = new(Week, 3, 7), StandartPlan = new(Standart, 5, 30), ProPlan = new(Pro, 7, 30), PremiumPlan = new(Premium, 10, 30);

    private static PeriodItem Period(PlanTarget plan, DateTimeOffset start, DateTimeOffset end) => new(plan.PlanId, plan.Price, plan.DurationDays, start, end);

    [Fact]
    public void First_purchase_starts_now()
    {
        var change = BillingRules.Decide([], ProPlan, 2, 0, Now);
        Assert.Equal(PlanChangeKind.New, change.Kind);
        Assert.Equal(Now, change.StartsAt);
        Assert.Equal(Now.AddDays(60), change.EndsAt);
    }

    [Fact]
    public void Ended_periods_are_ignored()
    {
        var change = BillingRules.Decide([Period(ProPlan, Now.AddDays(-40), Now.AddDays(-10))], StandartPlan, 1, 0, Now);
        Assert.Equal(PlanChangeKind.New, change.Kind);
        Assert.Equal(Now, change.StartsAt);
    }

    [Fact]
    public void Same_package_is_appended()
    {
        var end = Now.AddDays(10);
        var change = BillingRules.Decide([Period(ProPlan, Now.AddDays(-20), end)], ProPlan, 1, 0, Now);
        Assert.Equal(PlanChangeKind.Extend, change.Kind);
        Assert.Equal(end, change.StartsAt);
        Assert.Equal(end.AddDays(30), change.EndsAt);
    }

    [Fact]
    public void Cheaper_package_waits_for_the_current_one()
    {
        var end = Now.AddDays(10);
        var change = BillingRules.Decide([Period(ProPlan, Now.AddDays(-20), end)], WeekPlan, 1, 0, Now);
        Assert.Equal(PlanChangeKind.Downgrade, change.Kind);
        Assert.Equal(end, change.StartsAt);
        Assert.Equal(end.AddDays(7), change.EndsAt);
        Assert.Equal(0, change.BonusDays);
    }

    [Fact]
    public void Upgrade_starts_now_and_converts_unused_value_to_days()
    {
        // 15 days of Standart (5 AZN / 30 days) = 2.50 AZN; Pro costs 7/30 a day → 10.7 → 10 bonus days.
        var change = BillingRules.Decide([Period(StandartPlan, Now.AddDays(-15), Now.AddDays(15))], ProPlan, 1, 0, Now);
        Assert.Equal(PlanChangeKind.Upgrade, change.Kind);
        Assert.Equal(Now, change.StartsAt);
        Assert.Equal(10, change.BonusDays);
        Assert.Equal(Now.AddDays(40), change.EndsAt);
    }

    [Fact]
    public void Upgrade_counts_scheduled_periods_too()
    {
        // Running Pro (10 days left = 2.33) + scheduled Standart (30 days = 5.00) = 7.33 AZN → Premium 10/30 a day → 22 days.
        var proEnd = Now.AddDays(10);
        var chain = new[] { Period(ProPlan, Now.AddDays(-20), proEnd), Period(StandartPlan, proEnd, proEnd.AddDays(30)) };
        var change = BillingRules.Decide(chain, PremiumPlan, 1, 0, Now);
        Assert.Equal(PlanChangeKind.Upgrade, change.Kind);
        Assert.Equal(22, change.BonusDays);
    }

    [Fact]
    public void Value_not_day_count_moves_on_upgrade()
    {
        // A cheap package can never be turned into more days of a dearer one than its price buys.
        var bonus = BillingRules.BonusDays([Period(WeekPlan, Now, Now.AddDays(7))], PremiumPlan, Now);
        Assert.Equal(9, bonus); // 3 AZN of value, Premium = 0.333 AZN a day
        Assert.Equal(0, BillingRules.BonusDays([Period(ProPlan, Now, Now.AddDays(30))], new PlanTarget(Guid.NewGuid(), 0, 30), Now));
    }

    [Fact]
    public void Codes_are_short_and_unambiguous()
    {
        var code = BillingRules.NewCode(new Random(1));
        Assert.Matches("^EXR-[A-HJ-NP-Z2-9]{4}$", code);
    }

    [Fact]
    public void WhatsApp_link_carries_an_encoded_message()
    {
        var message = BillingRules.RequestMessage("İmtahan həftəsi", "EXR-7K3Q", 7, 3);
        Assert.Equal("Salam! «İmtahan həftəsi» paketini almaq istəyirəm. Kod: EXR-7K3Q, müddət: 7 gün, məbləğ: 3.00 AZN.", message);
        var link = BillingRules.WhatsAppLink("994501234567", message)!;
        Assert.StartsWith("https://wa.me/994501234567?text=Salam%21%20", link);
        Assert.DoesNotContain(" ", link);
        Assert.Null(BillingRules.WhatsAppLink(null, message));
        Assert.Equal("https://ig.me/m/examr.az", BillingRules.InstagramLink("examr.az"));
    }
}

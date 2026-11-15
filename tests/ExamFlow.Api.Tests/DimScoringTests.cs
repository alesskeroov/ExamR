using ExamFlow.Api.Features.Dim;
using ExamFlow.Api.Features.Exams;
using ExamFlow.Api.Features.Questions;

namespace ExamFlow.Api.Tests;

public class DimScoringTests
{
    private static IEnumerable<DimGradedItem> Items(string subject, int count, ExamOutcome outcome, string? topic = null) =>
        Enumerable.Repeat(new DimGradedItem(subject, topic, outcome), count);

    [Fact]
    public void Buraxilis_closed_part_uses_the_official_scale_and_reports_an_estimate()
    {
        var score = DimScoring.Score(DimTrack.Buraxilis, null, Items("az", 20, ExamOutcome.Correct).ToList());

        var az = Assert.Single(score.Subjects);
        Assert.Equal(50m, az.ClosedPoints);     // 20 closed × 100/40
        Assert.Equal(50m, az.ClosedMaxPoints);
        Assert.Equal(100m, az.EstimatedPoints);
        Assert.Equal(100m, az.FullMaxPoints);
    }

    [Fact]
    public void Buraxilis_has_no_penalty_and_scales_a_shorter_test_to_the_official_count()
    {
        var items = Items("riy", 5, ExamOutcome.Correct).Concat(Items("riy", 5, ExamOutcome.Incorrect)).ToList();

        var riy = Assert.Single(DimScoring.Score(DimTrack.Buraxilis, null, items).Subjects);

        Assert.Equal(20.3m, riy.ClosedPoints);  // 0.5 × 13 closed × 100/32
        Assert.Equal(40.6m, riy.ClosedMaxPoints);
        Assert.Equal(50m, riy.EstimatedPoints);
    }

    [Fact]
    public void Blok_wrong_answers_cost_a_quarter_and_group_coefficients_apply()
    {
        var items = Items("riy", 20, ExamOutcome.Correct).Concat(Items("riy", 2, ExamOutcome.Incorrect))
            .Concat(Items("kim", 22, ExamOutcome.Correct)).ToList();

        var score = DimScoring.Score(DimTrack.Blok, "I-RK", items);

        var riy = score.Subjects.Single(s => s.Subject == "riy");
        Assert.Equal(1.5m, riy.Coefficient);
        Assert.Equal(88.6m, riy.ClosedPoints);   // 19.5 × 100/33 × 1.5
        Assert.Equal(100m, riy.ClosedMaxPoints);
        Assert.Equal(133m, riy.EstimatedPoints);  // 19.5/22 × 150
        var kim = score.Subjects.Single(s => s.Subject == "kim");
        Assert.Equal(1m, kim.Coefficient);
        Assert.Equal(66.7m, kim.ClosedPoints);
        Assert.Equal(250m, score.FullMaxPoints);
    }

    [Fact]
    public void Open_coded_answers_fill_the_coded_part_of_mathematics()
    {
        var items = Items("riy", 13, ExamOutcome.Correct)
            .Concat(Enumerable.Repeat(new DimGradedItem("riy", null, ExamOutcome.Correct, IsOpen: true), 3))
            .Concat(Enumerable.Repeat(new DimGradedItem("riy", null, ExamOutcome.Incorrect, IsOpen: true), 2))
            .ToList();

        var riy = Assert.Single(DimScoring.Score(DimTrack.Buraxilis, null, items).Subjects);

        Assert.Equal(40.6m, riy.ClosedPoints);   // 13 × 100/32
        Assert.Equal(9.4m, riy.OpenPoints);      // 3/5 × 5 coded × 100/32
        Assert.Equal(15.6m, riy.OpenMaxPoints);
        Assert.Equal(5, riy.OpenTotal);
        Assert.Equal(3, riy.OpenCorrect);
        Assert.Equal(50m, riy.MeasuredPoints);
        Assert.Equal(56.3m, riy.MeasuredMaxPoints);
        Assert.Equal(88.9m, riy.EstimatedPoints); // 50 / 56.25 × 100
    }

    [Fact]
    public void Open_answers_of_a_subject_without_coded_tasks_stand_for_its_written_tasks()
    {
        var items = Enumerable.Repeat(new DimGradedItem("az", null, ExamOutcome.Correct, IsOpen: true), 4).ToList();

        var az = Assert.Single(DimScoring.Score(DimTrack.Buraxilis, null, items).Subjects);

        Assert.Equal(0m, az.ClosedMaxPoints);
        Assert.Equal(50m, az.OpenMaxPoints);      // 10 written × 2 × 100/40
        Assert.Equal(100m, az.EstimatedPoints);
    }

    [Fact]
    public void Net_score_never_goes_below_zero_and_ungraded_questions_are_left_out()
    {
        var items = Items("fiz", 1, ExamOutcome.Correct).Concat(Items("fiz", 10, ExamOutcome.Incorrect))
            .Concat(Items("fiz", 3, ExamOutcome.Ungraded)).ToList();

        var fiz = Assert.Single(DimScoring.Score(DimTrack.Blok, null, items).Subjects);

        Assert.Equal(0m, fiz.ClosedPoints);
        Assert.Equal(3, fiz.Ungraded);
        Assert.Equal(14, fiz.Total);
    }

    [Fact]
    public void Weak_topics_are_those_below_the_threshold_worst_first()
    {
        var items = Items("tar", 1, ExamOutcome.Correct, "Səfəvilər").Concat(Items("tar", 3, ExamOutcome.Incorrect, "Səfəvilər"))
            .Concat(Items("tar", 2, ExamOutcome.Unanswered, "Qarabağ xanlığı"))
            .Concat(Items("tar", 4, ExamOutcome.Correct, "Atabəylər")).Concat(Items("tar", 1, ExamOutcome.Incorrect, "Atabəylər"))
            .ToList();

        var weak = DimScoring.Score(DimTrack.Blok, "II", items).WeakTopics;

        Assert.Equal(["Qarabağ xanlığı", "Səfəvilər"], weak.Select(w => w.Topic));
        Assert.Equal(25m, weak[1].Accuracy);
    }

    [Fact]
    public void Answer_key_accepts_common_formats_and_reports_conflicts()
    {
        var (key, conflicts) = DimScoring.ParseAnswerKey("1-A 2) c, 3.B\n4 D 10E 11–e 12-F 13:a\n13-B");

        Assert.Equal(0, key[1]);
        Assert.Equal(2, key[2]);
        Assert.Equal(1, key[3]);
        Assert.Equal(3, key[4]);
        Assert.Equal(4, key[10]);
        Assert.Equal(4, key[11]);
        Assert.False(key.ContainsKey(12));
        Assert.Equal([13], conflicts);
    }

    [Theory]
    [InlineData(DimTrack.Buraxilis, DimTestKind.Full, null, new[] { "az", "riy", "xdil" }, true)]
    [InlineData(DimTrack.Buraxilis, DimTestKind.Full, null, new[] { "az", "riy" }, false)]
    [InlineData(DimTrack.Buraxilis, DimTestKind.Subject, null, new[] { "fiz" }, false)]
    [InlineData(DimTrack.Blok, DimTestKind.Full, "III-DT", new[] { "edb", "az", "tar" }, true)]
    [InlineData(DimTrack.Blok, DimTestKind.Full, "III-DT", new[] { "az", "tar", "cog" }, false)]
    [InlineData(DimTrack.Blok, DimTestKind.Full, null, new[] { "riy", "fiz", "kim" }, false)]
    [InlineData(DimTrack.Blok, DimTestKind.Subject, null, new[] { "bio" }, true)]
    [InlineData(DimTrack.Blok, DimTestKind.Subject, null, new[] { "bio", "kim" }, false)]
    public void Test_shape_must_match_the_official_structure(DimTrack track, DimTestKind kind, string? group, string[] subjects, bool valid)
    {
        var request = new DimTestRequest
        {
            Track = track, Kind = kind, GroupCode = group, Title = "Sınaq", DurationMinutes = 60,
            Sections = subjects.Select(s => new DimSectionRequest { Subject = s, RandomCount = 5 }).ToList(),
        };

        Assert.Equal(valid, DimAdminEndpoints.ValidateShape(request) is null);
    }

    [Theory]
    [InlineData(5, 1, true)]
    [InlineData(6, 1, false)]
    [InlineData(4, 0, false)]
    [InlineData(4, 2, false)]
    [InlineData(0, 0, false)]
    public void Only_closed_questions_with_one_known_answer_enter_the_bank(int options, int correct, bool accepted)
    {
        var question = new Question
        {
            Text = "Sual",
            Options = Enumerable.Range(0, options).Select(i => new AnswerOption { Text = $"V{i}", IsCorrect = i < correct }).ToList(),
        };

        Assert.Equal(accepted, DimAdminEndpoints.BankRejection(question) is null);
    }
}

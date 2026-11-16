using ExamFlow.Api.Features.Dim;

namespace ExamFlow.Api.Tests;

public class DimSubjectRunsTests
{
    private static DimAttemptItem Item(int position, string subject) => new() { Position = position, Subject = subject };

    [Fact]
    public void Full_test_positions_are_grouped_by_subject_in_exam_order()
    {
        var items = new[] { Item(3, "riy"), Item(0, "az"), Item(1, "az"), Item(2, "riy"), Item(4, "xdil") };

        var runs = DimEndpoints.SubjectRuns(items);

        Assert.Equal(
            [new DimSubjectRunDto("az", "Azərbaycan dili", 0, 2), new DimSubjectRunDto("riy", "Riyaziyyat", 2, 2),
             new DimSubjectRunDto("xdil", "Xarici dil (İngilis dili)", 4, 1)],
            runs);
    }

    [Fact]
    public void A_gap_in_positions_starts_a_new_run()
    {
        var runs = DimEndpoints.SubjectRuns([Item(0, "az"), Item(2, "az")]);

        Assert.Equal([new DimSubjectRunDto("az", "Azərbaycan dili", 0, 1), new DimSubjectRunDto("az", "Azərbaycan dili", 2, 1)], runs);
    }
}

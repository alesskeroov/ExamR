using ExamFlow.Api.Features.Learning;

namespace ExamFlow.Api.Tests;

/// <summary>Document analysis fixes: grouped headings sharing one answer, kept-but-unusable questions, topic grouping.</summary>
public class DocumentAnalysisTests
{
    private const string Long = "This combined answer explains every listed question in enough detail for the exam.";

    private static List<LearningDocumentChunk> Doc(params string[] paragraphs) =>
        [new() { Ordinal = 0, Text = string.Join("\n\n", paragraphs), PageFrom = 1, PageTo = 1 }];

    private static ParsedQuestionInfo Open(int n) => new(n, 0, false, false, null);

    [Fact]
    public void Question_headings_listed_together_share_the_answer_written_under_them()
    {
        // 8, 9 and 10 are written one under another, then one answer for all three (as in the reported Word file).
        var scan = OpenQuestionExtractor.Scan(Doc("7. Seven?", "Answer seven is written here in full.", "8. Eight?", "9. Nine?", "10. Ten?", Long, "11. Eleven?", "Answer eleven is written here in full."));
        Assert.Equal(Long, scan.Answers[8]);
        Assert.Equal(Long, scan.Answers[9]);
        Assert.Equal(Long, scan.Answers[10]);
        Assert.Equal([8, 9], scan.SharedAnswers!.Order());
        Assert.Equal("Answer seven is written here in full.", scan.Answers[7]);
    }

    [Fact]
    public void A_long_bare_question_list_does_not_take_the_text_after_it()
    {
        var headings = Enumerable.Range(1, 7).Select(n => $"{n}. Question {n}?").ToArray();
        var scan = OpenQuestionExtractor.Scan(Doc([.. headings, Long]));
        Assert.All(Enumerable.Range(1, 6), n => Assert.Equal("", scan.Answers[n]));
        Assert.Empty(scan.SharedAnswers!);
    }

    [Fact]
    public void Unanswered_questions_are_kept_with_a_reason_but_never_used()
    {
        var index = OpenQuestionClassifier.Classify(Doc("1. One?", Long, "2. Two?", Long + " Two.", "3. Three?", Long + " Three.", "4. Four?"),
            [Open(1), Open(2), Open(3), Open(4)]);
        Assert.Equal([1, 2, 3], index.Questions.Select(q => q.Number));
        var kept = Assert.Single(index.Excluded!);
        Assert.Equal((4, ExclusionReason.NoAnswer), (kept.Number, kept.Reason));
        Assert.Equal(1, index.UnansweredCount);
        Assert.Contains("saxlanılıb", index.Warning);
    }

    [Fact]
    public void Shared_answers_are_marked_on_the_usable_questions()
    {
        var index = OpenQuestionClassifier.Classify(Doc("1. One?", Long, "2. Two?", "3. Three?", Long + " Shared.", "4. Four?", Long + " Four."),
            [Open(1), Open(2), Open(3), Open(4)]);
        Assert.Equal(4, index.Questions.Count);
        Assert.True(index.Questions.Single(q => q.Number == 2).SharedAnswer);
        Assert.False(index.Questions.Single(q => q.Number == 3).SharedAnswer);
        Assert.Empty(index.Excluded!);
    }

    [Fact]
    public void A_list_inside_an_answer_is_not_a_test_in_an_open_document()
    {
        // The parser saw options under question 2 but no marked answer, and the document has no real test question.
        var paragraphs = Enumerable.Range(1, 6).SelectMany(n => new[] { $"{n}. Question {n}?", $"{Long} Number {n}." }).ToArray();
        var index = OpenQuestionClassifier.Classify(Doc(paragraphs), [Open(1), new(2, 3, false, false, null), Open(3), Open(4), Open(5), Open(6)]);
        Assert.Equal(OpenDocumentKind.Open, index.Kind);
        Assert.Equal(6, index.Questions.Count);
        Assert.Empty(index.Excluded!);
    }

    [Fact]
    public void An_unmarked_test_stays_unclear_and_marked_tests_stay_excluded()
    {
        var paragraphs = Enumerable.Range(1, 6).SelectMany(n => new[] { $"{n}. Question {n}?", $"{Long} Number {n}." }).ToArray();
        // Most questions have options without a marked answer: not "lists in answers"; they stay unclear, so the document
        // (only 2 open questions left) is not accepted as an open-question document at all.
        var unmarked = OpenQuestionClassifier.Classify(Doc(paragraphs), [.. Enumerable.Range(1, 4).Select(n => new ParsedQuestionInfo(n, 4, false, false, null)), Open(5), Open(6)]);
        Assert.Equal(OpenDocumentKind.NoOpenQuestions, unmarked.Kind);
        Assert.Empty(unmarked.Questions);
        Assert.Equal(4, unmarked.UnclearCount);
        // A mixed document: the marked test question is excluded as a test, the unmarked one stays unclear.
        var mixed = OpenQuestionClassifier.Classify(Doc(paragraphs), [Open(1), Open(2), Open(3), new(4, 4, true, false, null), new(5, 4, false, false, null), Open(6)]);
        Assert.Equal(OpenDocumentKind.Mixed, mixed.Kind);
        Assert.Contains(mixed.Excluded!, e => e is { Number: 4, Reason: ExclusionReason.MultipleChoice });
        Assert.Contains(mixed.Excluded!, e => e is { Number: 5, Reason: ExclusionReason.Unclear });
    }

    [Fact]
    public void Repeated_questions_are_kept_once_per_number()
    {
        var scan = OpenQuestionExtractor.Scan(Doc("1. Same question?", Long, "2. Same question?", Long + " Again.", "3. Other?", Long + " Other.", "2. Same question?", "Stray repeat."));
        Assert.Equal([1, 2, 3], scan.Questions.Select(q => q.Number));
        Assert.Equal(Long + " Again.", scan.Answers[2]);
    }

    [Fact]
    public void Grouping_never_loses_or_duplicates_a_question()
    {
        // 3 is claimed by two topics (close to both), 9 is unknown, 5 was forgotten.
        var groups = StudyJson.NormalizeGroups([("AI", "Search", [1, 2, 3]), ("AI", "Planning", [3, 4, 9])], [1, 2, 3, 4, 5], n => $"Q{n}");
        Assert.Equal([1, 2, 3, 4, 5], groups.SelectMany(g => g.Numbers).Order());
        Assert.Equal([3], groups[0].Numbers.Intersect([3]));
        Assert.Equal(("", "Q5"), (groups[^1].Section, groups[^1].Title));
    }

    [Fact]
    public void Duplicate_topics_are_merged_only_within_the_same_main_topic()
    {
        var merged = StudyJson.MergeSimilar([
            ("Süni intellekt", "Axtarış alqoritmləri", [1, 2]),
            ("Bilik təqdimatı", "Semantik şəbəkələr", [3]),
            ("Süni intellekt", "Axtarış alqoritmləri", [4]),          // exact duplicate (e.g. from another slice)
            ("Süni intellekt", "Axtarış alqoritmləri və növləri", [5]), // different subtopic title: kept apart
            ("Məlumat bazası", "Axtarış alqoritmləri", [6]),          // same title, other main topic: kept apart
        ]);
        Assert.Equal(4, merged.Count);
        Assert.Equal([1, 2, 4], merged[0].Numbers);
        Assert.Equal([1, 2, 3, 4, 5, 6], merged.SelectMany(g => g.Numbers).Order());
    }

    [Fact]
    public void Grouping_asks_for_meaning_and_main_topics_without_a_fixed_count()
    {
        Assert.Contains("MEANING", StudyPrompts.GroupTask);
        Assert.Contains("no target number of groups", StudyPrompts.GroupTask);
        Assert.Contains("section", StudyPrompts.GroupSchema()["properties"]!["groups"]!["items"]!["required"]!.ToJsonString());
    }

    [Fact]
    public void Grading_judges_only_this_question_part_of_a_shared_answer() =>
        Assert.Contains("several related questions", StudyPrompts.GradeTask);

    [Theory]
    [InlineData("pdf", true)]
    [InlineData("docx", true)]   // an existing Word chat keeps working
    [InlineData("txt", false)]
    [InlineData("doc", false)]
    public void Tutor_formats(string format, bool supported) => Assert.Equal(supported, OpenQuestionIndexer.IsSupportedFormat(format));
}

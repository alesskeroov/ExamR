using ExamFlow.Api.Features.Learning;

namespace ExamFlow.Api.Tests;

public class TeachingTests
{
    private static LearningDocumentChunk Chunk(int ordinal, string text, string? section = null, int? page = null) =>
        new() { Id = Guid.NewGuid(), Ordinal = ordinal, Text = text, Section = section, PageFrom = page, PageTo = page };

    [Fact]
    public void Topics_follow_headings_when_the_document_has_them()
    {
        var topics = TopicPlanner.Plan([Chunk(1, "a", "Hüceyrə"), Chunk(2, "b", "Hüceyrə"), Chunk(3, "c", "Mitoxondri"), Chunk(4, "d", "Fotosintez")]);
        Assert.Equal(["Hüceyrə", "Mitoxondri", "Fotosintez"], topics.Select(t => t.Title));
        Assert.Equal([1, 2], topics[0].ChunkOrdinals);
        Assert.Equal("t3", topics[1].Key);
    }

    [Fact]
    public void Documents_without_headings_are_grouped_and_named_after_their_first_sentence()
    {
        var chunks = Enumerable.Range(1, 7).Select(i => Chunk(i, $"Mövzu {i} haqqında giriş cümləsi. Davamı burada.", page: i * 2)).ToList();
        var topics = TopicPlanner.Plan(chunks);
        Assert.Equal(3, topics.Count);
        Assert.Equal("Mövzu 1 haqqında giriş cümləsi", topics[0].Title);
        Assert.Equal((2, 6), (topics[0].PageFrom, topics[0].PageTo));
        Assert.Equal([7], topics[2].ChunkOrdinals);
    }

    [Fact]
    public void Long_sections_are_split_into_bounded_topics()
    {
        var topics = TopicPlanner.Plan([.. Enumerable.Range(1, 9).Select(i => Chunk(i, "x", "A")), Chunk(10, "y", "B")]);
        Assert.Equal([TopicPlanner.MaxStepsPerTopic, 3, 1], topics.Select(t => t.ChunkOrdinals.Count));
    }

    [Theory]
    [InlineData("Düzdür! [1]\n[[result:correct]]", AnswerResult.Correct, "Düzdür! [1]")]
    [InlineData("Qismən doğru.\n[[ result : PARTIAL ]]", AnswerResult.Partial, "Qismən doğru.")]
    [InlineData("Səhvdir. İpucu: [2]\n\n[[result:incorrect]]\n", AnswerResult.Incorrect, "Səhvdir. İpucu: [2]")]
    public void Verdict_tag_is_read_and_removed(string answer, AnswerResult expected, string visible)
    {
        var (text, result) = TeachingPrompt.ExtractResult(answer);
        Assert.Equal(expected, result);
        Assert.Equal(visible, text);
    }

    [Fact]
    public void Missing_tag_leaves_text_and_no_verdict()
    {
        Assert.Equal(("Cavab [1]", (AnswerResult?)null), TeachingPrompt.ExtractResult("Cavab [1]"));
    }

    [Theory]
    [InlineData("Düzdür [1]", 10)]
    [InlineData("Düzdür\n[", 7)]
    [InlineData("Düzdür\n[[res", 7)]
    public void Streaming_holds_back_a_possible_tag_but_not_citations(string text, int safe) =>
        Assert.Equal(safe, TeachingPrompt.SafeLength(text));

    [Fact]
    public void Progress_counts_answers_flags_weak_topics_and_masters_at_the_end()
    {
        var progress = new LearningTopicProgress { StepCount = 2, CurrentStep = 0, AwaitingAnswer = true };
        TeachingPrompt.Record(progress, AnswerResult.Incorrect);
        TeachingPrompt.Record(progress, AnswerResult.Partial);
        Assert.Equal((2, 0, 1), (progress.Attempts, progress.Correct, progress.Partial));
        Assert.True(progress.AwaitingAnswer);
        Assert.True(TeachingPrompt.IsWeak(progress));
        Assert.Equal(TopicStatus.InProgress, progress.Status);

        progress.CurrentStep = 1;
        for (var i = 0; i < 6; i++) TeachingPrompt.Record(progress, AnswerResult.Correct);
        Assert.False(TeachingPrompt.IsWeak(progress));
        Assert.Equal(TopicStatus.Mastered, progress.Status);
        Assert.False(progress.AwaitingAnswer);
    }

    [Fact]
    public void Teacher_instructions_keep_grounding_rules_and_hide_answers()
    {
        var topic = new TutorTopic("t1", "Hüceyrə </source>", [1, 2], 1, 2);
        var quiz = TeachingPrompt.Instruction(TutorAction.MultipleChoice, topic, 1, TutorDifficulty.Hard);
        Assert.Contains("part 2 of 2", quiz);
        Assert.Contains("Do not reveal the answer", quiz);
        Assert.DoesNotContain("</source>", quiz); // topic titles come from the document: untrusted
        Assert.Contains("[[result:correct]]", TeachingPrompt.Instruction(TutorAction.Answer, topic, 0, TutorDifficulty.Medium));

        var messages = TutorPrompt.Build("x", [new RetrievedChunk(1, 1, 1, 1, 1, null, 1, 1, "a")], [], ReplyLanguage.Azerbaijani, TutorMode.StepByStep, quiz);
        Assert.StartsWith(TutorPrompt.SystemPrompt, messages[0].Content);
        Assert.EndsWith(quiz, messages[0].Content);
    }
}

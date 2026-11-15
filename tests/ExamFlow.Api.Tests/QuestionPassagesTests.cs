using ExamFlow.Api.Features.Questions;

namespace ExamFlow.Api.Tests;

public class QuestionPassagesTests
{
    [Fact]
    public void Questions_of_one_passage_are_placed_together_in_source_order()
    {
        var passage = new QuestionPassage { Id = Guid.NewGuid(), FromNumber = 18, ToNumber = 21 };
        var questions = new[] { 5, 20, 7, 18, 19 }.ToDictionary(_ => Guid.NewGuid(), n => new Question { SourceNumber = n });
        foreach (var (id, question) in questions) question.Id = id;
        var ids = questions.Keys.ToList();
        var passages = questions.Where(q => q.Value.SourceNumber >= 18).ToDictionary(q => q.Key, _ => passage);

        var ordered = QuestionPassages.KeepTogether(ids, passages, questions);

        Assert.Equal([5, 18, 19, 20, 7], ordered.Select(id => questions[id].SourceNumber));
    }
}

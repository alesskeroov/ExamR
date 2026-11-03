using ExamFlow.Api.Features.Learning;

namespace ExamFlow.Api.Tests;

public class OpenQuestionExtractorTests
{
    private static LearningDocumentChunk Chunk(string text, int ordinal = 1) =>
        new() { Ordinal = ordinal, Text = text, PageFrom = ordinal, PageTo = ordinal };

    [Theory]
    [InlineData(5)]
    [InlineData(35)]
    [InlineData(120)]
    public void Extracts_every_numbered_question_and_its_answer(int count)
    {
        var text = string.Join("\n\n", Enumerable.Range(1, count).Select(i =>
            $"{i}.Sual {i} haqqında məlumat verin.\n\nBu mövzunun cavabı sənəddə ətraflı izah olunur və əsas anlayışları nümunələrlə təqdim edir."));
        var scan = OpenQuestionExtractor.Scan([Chunk(text)]);
        Assert.Equal(Enumerable.Range(1, count), scan.Questions.Select(q => q.Number));
        Assert.All(scan.Questions, q => Assert.True(q.HasAnswer));
        Assert.False(scan.LooksMultipleChoice);
    }

    [Fact]
    public void Keeps_numbered_answer_lists_inside_the_parent_answer()
    {
        var scan = OpenQuestionExtractor.Scan([Chunk("""
            1. Şəbəkəni izah edin.

            Şəbəkənin əsas hissələri bunlardır:

            1. Server məlumatları saxlayır.

            2. Müştəri sorğuları göndərir.

            3. Router paketləri yönləndirir.

            2. Protokolu izah edin.

            Protokol məlumat mübadiləsi qaydalarını müəyyən edir.
            Qaydalar bütün iştirakçılar üçün ortaq olur.

            3. Ünvanlama nədir?

            Ünvanlama şəbəkədə cihazların fərqləndirilməsi üçün istifadə edilir.
            """)]);
        Assert.Equal([1, 2, 3], scan.Questions.Select(q => q.Number));
        Assert.Contains("3. Router", scan.Answers[1]);
        Assert.Contains("Qaydalar bütün", scan.Answers[2]);
    }

    [Fact]
    public void Question_only_documents_do_not_claim_to_have_answers()
    {
        var scan = OpenQuestionExtractor.Scan([Chunk("1. Birinci mövzu nədir?\n\n2. İkinci mövzu nədir?\n\n3. Üçüncü mövzu nədir?")]);
        Assert.Equal(3, scan.Questions.Count);
        Assert.All(scan.Questions, q => Assert.False(q.HasAnswer));
    }

    [Fact]
    public void Recognizes_multiple_choice_documents()
    {
        var scan = OpenQuestionExtractor.Scan([Chunk(string.Join("\n\n", Enumerable.Range(1, 5)
            .Select(i => $"{i}. Hansı cavab doğrudur?\nA) Birinci\nB) İkinci\nC) Üçüncü")))]);
        Assert.True(scan.LooksMultipleChoice);
    }

    [Fact]
    public void Removes_overlapping_chunk_paragraphs_and_preserves_source_page()
    {
        var scan = OpenQuestionExtractor.Scan([
            Chunk("1. Birinci mövzu nədir?\n\n2. İkinci mövzu nədir?"),
            Chunk("2. İkinci mövzu nədir?\n\n3. Üçüncü mövzu nədir?", 2)]);
        Assert.Equal([1, 2, 3], scan.Questions.Select(q => q.Number));
        Assert.Equal(2, scan.Questions[2].Page);
    }

    [Fact]
    public void Unnumbered_questions_use_sentence_fallback()
    {
        var scan = OpenQuestionExtractor.Scan([Chunk("Şəbəkələr necə işləyir?\n\nProtokollar niyə lazımdır?")]);
        Assert.Equal(2, scan.Questions.Count);
        Assert.All(scan.Questions, q => Assert.False(q.HasAnswer));
    }
}

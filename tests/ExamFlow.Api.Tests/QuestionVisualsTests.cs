using ExamFlow.Api.Features.Exams;
using ExamFlow.Api.Features.Questions;

namespace ExamFlow.Api.Tests;

public class QuestionVisualsTests
{
    private static readonly AnswerOption[] Options =
    [
        new() { Id = Guid.NewGuid(), Position = 0, Label = "A", Text = "." },
        new() { Id = Guid.NewGuid(), Position = 1, Label = "B", Text = "." },
    ];

    private static QuestionVisual Visual(byte? optionPosition, string? storageKey = "qv_x.png") => new()
    {
        Id = Guid.NewGuid(),
        OptionPosition = optionPosition,
        PageNumber = 3,
        Width = 120,
        Height = 40,
        StorageKey = storageKey,
        ContentType = storageKey is null ? null : "image/png",
        MappingStatus = QuestionVisualStatus.AutoMapped,
    };

    [Fact]
    public void Snapshot_links_option_images_by_option_id_so_shuffling_keeps_them()
    {
        var snapshot = QuestionVisuals.ToSnapshot(Visual(1), Options);

        Assert.Equal(Options[1].Id, snapshot.OptionId);
        Assert.Equal("qv_x.png", snapshot.StorageKey);
    }

    [Fact]
    public void Body_image_has_no_option()
    {
        Assert.Null(QuestionVisuals.ToSnapshot(Visual(null), Options).OptionId);
        Assert.Null(QuestionVisuals.ToDto(Visual(null), [Options[0].Id, Options[1].Id]).OptionId);
    }

    [Fact]
    public void Dto_never_exposes_the_storage_key_and_flags_missing_files()
    {
        var withFile = QuestionVisuals.ToDto(QuestionVisuals.ToSnapshot(Visual(0), Options));
        var withoutFile = QuestionVisuals.ToDto(Visual(0, storageKey: null), [Options[0].Id]);

        Assert.True(withFile.HasFile);
        Assert.Equal(Options[0].Id, withFile.OptionId);
        Assert.False(withoutFile.HasFile);
        Assert.DoesNotContain(typeof(VisualDto).GetProperties(), p => p.Name.Contains("Key", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Option_position_beyond_the_options_falls_back_to_the_question_body()
    {
        Assert.Null(QuestionVisuals.ToDto(Visual(5), [Options[0].Id]).OptionId);
    }

    [Fact]
    public void Exams_created_before_images_have_no_visuals()
    {
        var snapshot = new QuestionSnapshot { Text = "Q", Visuals = null };

        Assert.Null(snapshot.Visuals);
    }
}

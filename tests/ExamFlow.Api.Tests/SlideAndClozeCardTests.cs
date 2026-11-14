using ExamFlow.Api.Features.Learning;

namespace ExamFlow.Api.Tests;

public class SlideAndClozeCardTests
{
    private static DocParagraph Title(string text, int slide) => new(text, slide, 0) { HeadingLevel = 1 };
    private static DocParagraph Bullet(string text, int slide, int level = 0) => new(text, slide, 0) { Level = level };

    [Fact]
    public void A_slide_gives_a_title_to_bullets_card_with_sub_bullets_joined()
    {
        var deck = NotesCardBuilder.Build([
            Title("Hidridlərin növləri", 1),
            Bullet("İon hidridləri", 1), Bullet("NaH, CaH₂", 1, 1),
            Bullet("Kovalent hidridlər", 1), Bullet("Metal hidridləri", 1),
        ]);

        var card = Assert.Single(deck.Cards, c => c.Kind == CardKind.List);
        Assert.Equal("Hidridlərin növləri: əsas məqamlar hansılardır?", card.Front);
        Assert.Equal(["İon hidridləri — NaH, CaH₂", "Kovalent hidridlər", "Metal hidridləri"], card.Items);
        Assert.Equal(1, card.Page);
    }

    [Fact]
    public void A_continued_slide_extends_the_same_card()
    {
        var deck = NotesCardBuilder.Build([
            Title("Turşu və əsaslar", 1), Bullet("Arrhenius nəzəriyyəsi", 1), Bullet("Brønsted–Lowry nəzəriyyəsi", 1),
            Title("Turşu və əsaslar (davamı)", 2), Bullet("Lewis nəzəriyyəsi", 2), Bullet("Neytrallaşma reaksiyası", 2),
        ]);

        var card = Assert.Single(deck.Cards, c => c.Kind == CardKind.List);
        Assert.Equal(4, card.Items.Count);
    }

    [Fact]
    public void Slides_with_one_point_or_a_generic_title_give_no_slide_card()
    {
        var deck = NotesCardBuilder.Build([
            Title("Giriş", 1), Bullet("Kursun məqsədi", 1), Bullet("Qiymətləndirmə", 1),
            Title("Elektronmənfilik", 2), Bullet("Atomun elektronları özünə çəkmə qabiliyyətidir.", 2),
        ]);

        Assert.DoesNotContain(deck.Cards, c => c.Kind == CardKind.List);
    }

    [Fact]
    public void A_bold_term_becomes_a_cloze_card()
    {
        var paragraph = new DocParagraph("Ənənəvi yanaşmada avadanlıq əvvəlcədən alınır. Bu, capex adlanır və böyük məbləğdə uzunmüddətli aktivə pul yatırmaqdır.", 3, 0)
            { Bold = ["capex"] };

        var (front, term) = Assert.Single(NotesCardBuilder.Clozes(paragraph));
        Assert.Equal("Bu, _____ adlanır və böyük məbləğdə uzunmüddətli aktivə pul yatırmaqdır.", front);
        Assert.Equal("capex", term);
    }

    [Theory]
    [InlineData("Materialdakı faktlar rəsmi sənədlərdən 12 oktyabr 2026 tarixində yoxlanılıb və yenilənib.", "12 oktyabr 2026")]
    [InlineData("Kompüter şəbəkələri: IPv4 ünvanı, alt şəbəkə maskası və /24 yazılışı burada lazımdır.", "Kompüter şəbəkələri")]
    [InlineData("Docker əmrləri burada yalnız oxuma nümunəsidir və bu material üçün icra edilməyib.", "oxuma nümunəsidir")]
    [InlineData("Elastiklik nədir və miqyaslanmadan nə ilə fərqlənir, nümunə ilə izah edin?", "Elastiklik")]
    [InlineData("Qısa cümlədə capex var.", "capex")]
    public void Dates_labels_predicates_questions_and_short_sentences_give_no_cloze(string text, string bold)
    {
        Assert.Empty(NotesCardBuilder.Clozes(new DocParagraph(text, 1, 0) { Bold = [bold] }));
    }

    [Fact]
    public void Cloze_cards_are_built_from_bold_paragraphs_but_not_in_generic_sections()
    {
        var deck = NotesCardBuilder.Build([
            new DocParagraph("1 Annotasiya", 1, 0) { HeadingLevel = 1 },
            new DocParagraph("Bu material bulud hesablamanın əsaslarını izah edir və səbəb-nəticə məntiqini öyrədir.", 1, 0) { Bold = ["səbəb-nəticə məntiqini"] },
            new DocParagraph("2 Virtuallaşdırma", 1, 0) { HeadingLevel = 1 },
            new DocParagraph("2000-ci illərdə virtuallaşdırma texnologiyası yetkinləşir və bir serveri bir neçə hissəyə bölür.", 1, 0) { Bold = ["virtuallaşdırma"] },
        ]);

        var card = Assert.Single(deck.Cards, c => c.Kind == CardKind.Cloze);
        Assert.Equal("virtuallaşdırma", card.Back);
    }
}

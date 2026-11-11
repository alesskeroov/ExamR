using ExamFlow.Api.Features.Learning;

namespace ExamFlow.Api.Tests;

public class NotesCardBuilderTests
{
    private const string Filler = "Bu bölmə mövzunun əsas fikrini izah edir və tələbəyə sonrakı hissələri başa düşmək üçün lazım olan konteksti verir, nümunələrlə.";

    private static List<DocParagraph> Doc(params string[] lines) => lines.Select(l => new DocParagraph(l, 1, 0)).ToList();

    [Fact]
    public void Cards_stay_under_the_chapter_they_come_from()
    {
        // "1 …: …" has a colon and "2 …" has no body of its own: both used to be missed, so chapter 2's cards
        // were shown under chapter 1's title.
        var deck = NotesCardBuilder.Build(Doc(
            "1 Problemin mənşəyi: bir hostdan klasterə",
            Filler,
            "Pod — Kubernetes-in ən kiçik yerləşdirmə vahididir.",
            "2 Təhlükəsizlik və xərc",
            "2.1 Şəbəkə siyasəti",
            "NetworkPolicy — Pod-lar arasında trafiki məhdudlaşdıran qaydalar toplusudur.",
            Filler));

        Assert.Equal(["Problemin mənşəyi: bir hostdan klasterə", "Təhlükəsizlik və xərc"], deck.Topics.Select(t => t.Title));
        var pod = Assert.Single(deck.Cards, c => c.Front == "Pod nədir?");
        Assert.Equal(deck.Topics[0].Key, pod.TopicKey);
        Assert.Null(pod.Section);
        var policy = Assert.Single(deck.Cards, c => c.Front == "NetworkPolicy nədir?");
        Assert.Equal(deck.Topics[1].Key, policy.TopicKey);
        Assert.Equal("Şəbəkə siyasəti", policy.Section);
    }

    [Fact]
    public void A_subsection_of_a_missed_chapter_opens_its_own_topic()
    {
        // "3 …" is too long to be a heading; "3.1 …" must still not land in chapter 2.
        var deck = NotesCardBuilder.Build(Doc(
            "2 Əsas anlayışlar",
            Filler,
            "3 Bu çox uzun sətir başlıq deyil çünki burada on dörddən çox söz var və mətn davam edir hələ də bitmir",
            "3.1 Marşrutlaşdırma cədvəli",
            "Marşrut cədvəli — paketin hansı növbəti addıma göndəriləcəyini müəyyən edən qaydalar siyahısıdır.",
            Filler));

        var card = Assert.Single(deck.Cards, c => c.Front == "Marşrut cədvəli nədir?");
        Assert.NotEqual(deck.Topics.Single(t => t.Title == "Əsas anlayışlar").Key, card.TopicKey);
    }

    [Fact]
    public void A_numbered_list_item_after_a_lead_is_not_a_heading()
    {
        var deck = NotesCardBuilder.Build(Doc(
            "1 Sorğunun yolu",
            Filler,
            "Sorğu aşağıdakı mərhələlərdən keçir:",
            "1. Brauzer: DNS sorğusu göndərir",
            "2. Server: cavabı qaytarır",
            Filler));

        Assert.Equal(["Sorğunun yolu"], deck.Topics.Select(t => t.Title));
    }

    [Fact]
    public void Questions_answered_together_make_one_card_without_the_leftover_fragment()
    {
        const string answer = "rdə öyrənmənin prosesi\nBiliklərin təqdimatı intellektual davranışın təməlidir və hər metodun öz üstünlükləri var.";
        var deck = CardDeckBuilder.FromOpenQuestions([
            new IndexedQuestion(8, "Bilik təqdimatı modelləri.", answer, 0, 1, SharedAnswer: true),
            new IndexedQuestion(9, "IF-THEN qaydalar toplusu.", answer, 0, 1, SharedAnswer: true),
            new IndexedQuestion(10, "Semantik şəbəkələr modeli.", answer, 0, 1),
            new IndexedQuestion(11, "Freymlər nədir?", "Freym obyekt haqqında bilikləri sahələr şəklində saxlayan strukturdur.", 0, 2),
        ]);

        Assert.Equal(["Bilik təqdimatı modelləri", "Freymlər nədir?"], deck.Topics.Select(t => t.Title));
        var shared = Assert.Single(deck.Cards, c => c.Kind == CardKind.Question && c.Source == 8);
        Assert.Equal(CardMastery.Hash("Bilik təqdimatı modelləri."), shared.Id);
        Assert.Equal("Bilik təqdimatı modelləri.\nIF-THEN qaydalar toplusu.\nSemantik şəbəkələr modeli.", shared.Front);
        Assert.StartsWith("Biliklərin təqdimatı", shared.Back);
        Assert.Equal(2, deck.Cards.Count(c => c.Kind == CardKind.Question));
    }

    [Fact]
    public void A_titled_section_gives_a_section_card_with_its_opening_sentences()
    {
        var deck = NotesCardBuilder.Build(Doc(
            "1 Virtual şəbəkə",
            "Virtual şəbəkə buludda kirayəçiyə məxsus məntiqi şəbəkədir və onun sərhədini provayder tətbiq edir. İçini isə müştəri layihələndirir. Ünvanlar ayrıca ayrılır. Trafik qaydalarla idarə edilir. Giriş nəzarətdə saxlanılır. Hadisələr jurnalda saxlanılır. Yeddinci cümlə karta düşmür.",
            "2 Komponentlər",
            "Virtual şəbəkə 10.20.0.0/16 bir region və iki zonadan ibarətdir, quruluşu aşağıda göstərilir (Şəkil 4).",
            "3 Nəticə",
            Filler));

        var card = Assert.Single(deck.Cards, c => c.IsSection);
        Assert.Equal(CardKind.Question, card.Kind);
        Assert.Equal("Virtual şəbəkə", card.Front);
        Assert.Equal("Virtual şəbəkə buludda kirayəçiyə məxsus məntiqi şəbəkədir və onun sərhədini provayder tətbiq edir. İçini isə müştəri layihələndirir. Ünvanlar ayrıca ayrılır. Trafik qaydalarla idarə edilir. Giriş nəzarətdə saxlanılır. Hadisələr jurnalda saxlanılır.", card.Back);
    }

    [Theory]
    [InlineData("PDO-nun vahid interfeys təqdim etməsinin nə üstünlüyü olduğunu şərh edin.")]
    [InlineData("Onların hansı hallarda seçildiyini əsaslandırın.")]
    [InlineData("Fərqləri yazın. İstifadəsini nümunələrlə açıqlayın.")]
    public void Instructions_mistaken_for_answers_do_not_make_cards(string answer)
    {
        var deck = CardDeckBuilder.FromOpenQuestions([new IndexedQuestion(1, "PDO nədir?", answer, 0, 1)]);
        Assert.Empty(deck.Cards);
        Assert.Null(NotesCardBuilder.Summary(Doc(answer)));
    }

    [Fact]
    public void A_prompt_before_a_real_answer_is_removed_without_changing_the_card_identity()
    {
        const string question = "PDO nədir?";
        const string answer = "PDO verilənlər bazalarına vahid interfeys vasitəsilə qoşulmağa imkan verən PHP genişlənməsidir.";
        var deck = CardDeckBuilder.FromOpenQuestions([new IndexedQuestion(1, question, "Üstünlükləri şərh edin.\n" + answer, 0, 1)]);
        var card = Assert.Single(deck.Cards);
        Assert.Equal(answer, card.Back);
        Assert.Equal(CardMastery.Hash(question), card.Id);
        Assert.Equal(question, card.Front);
        Assert.False(OpenQuestionExtractor.IsPromptOnly(answer + " İstifadəsini izah edin."));
    }

    [Fact]
    public void Html_tag_fragments_do_not_form_a_list_but_prose_about_tags_is_preserved()
    {
        Assert.Empty(CardDeckBuilder.Lists(["Aşağıdakı texnologiyalarla işləmək təcrübəm var:", "<ul>", "</section>"]));
        Assert.True(DocumentCleaner.IsCode("<head>"));
        Assert.True(DocumentCleaner.IsCode("</head>"));
        Assert.False(DocumentCleaner.IsCode("<head> teqi sənədin metadatasını saxlayır."));
        Assert.False(DocumentCleaner.IsCode("HTML sənədində <ul> siyahı yaratmaq üçün istifadə olunur."));
    }

    [Fact]
    public void Long_question_answers_remain_complete_in_the_deck()
    {
        var answer = string.Join(" ", Enumerable.Range(1, 8).Select(i => $"Şəbəkənin {i} nömrəli hissəsi məlumatların ötürülməsinə xidmət edir."));
        var deck = CardDeckBuilder.FromOpenQuestions([new IndexedQuestion(1, "Şəbəkə necə işləyir?", answer, 0, 1)]);
        Assert.Equal(answer, Assert.Single(deck.Cards).Back);
    }

    [Theory]
    [InlineData("burada - maksimal tezlik deviasiyası, - yuxarı modulyasiya tezliyidir və bu kəmiyyət cədvəldən götürülür.")]
    [InlineData("Lülənin buraxma qabiliyyəti tezlik deviasiyası ilə məhduddur və hesablanır. Təqribən .")]
    [InlineData("Bu bölmədə anlayışlar bir sistemdə birləşir və nümunə üzərində ardıcıl göstərilir.")]
    [InlineData("Qurğular təmir və yüksəldilmə imkanı ilə hazırlanmalıdırmı? Bu sual hər layihədə yenidən verilir.")]
    public void Section_summary_skips_text_that_needs_what_the_card_cannot_show(string paragraph) =>
        Assert.Null(NotesCardBuilder.Summary(Doc(paragraph)));

    [Theory]
    [InlineData("Brauzer HTML faylını oxuyur və ekranda göstərir. Bu prosesə render etmək deyilir.", "render etmək")]
    [InlineData("Resurslar tələbata görə avtomatik artırılır və ya azaldılır - bu xüsusiyyətə \"auto-scaling\" deyilir.", "auto-scaling")]
    [InlineData("Server istifadəçiyə unikal identifikator təyin edir. Bu identifikator Session ID və ya qısaca SID adlanır.", "Session ID (SID)")]
    public void A_term_named_after_bu_takes_the_sentence_before_as_its_explanation(string paragraph, string term)
    {
        var (found, definition) = Assert.Single(NotesCardBuilder.Namings(paragraph));
        Assert.Equal(term, found);
        Assert.EndsWith(".", definition);
    }

    [Theory]
    [InlineData("RRX-nın normal işini təmin edən avadanlıqlar kompleksi radiorele sistemi adlandırılır.")] // where the term starts is a guess
    [InlineData("Qradientlər eksponensial olaraq kiçilir və öyrənmə dayanır. Buna görə bu dövr “Sİ qışı” adlanır.")]
    [InlineData("Bu analiz nəticəsində sistem optimal parametrləri müəyyən edir. Bu proses optimallaşdırma adlanır.")]
    public void Unsure_namings_give_no_term(string paragraph) => Assert.Empty(NotesCardBuilder.Namings(paragraph));

    [Fact]
    public void A_heading_in_capitals_takes_the_spelling_the_body_uses()
    {
        var deck = NotesCardBuilder.Build(Doc(
            "IOT QURĞULARININ TƏHLÜKƏSIZLIYI",
            "Bu bölmədə qeyd olunur ki, IoT cihazları və onların qurğularının təhlükəsizliyi ənənəvi kompüterlərdən fərqlənir, çünki resursları azdır.",
            Filler));

        Assert.Equal("IoT qurğularının təhlükəsizliyi", deck.Topics[0].Title);
    }
}

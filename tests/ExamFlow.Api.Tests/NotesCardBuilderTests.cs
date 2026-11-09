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
}

using System.Net;

namespace ExamFlow.Api.Infrastructure.Email;

/// <summary>
/// ExamR transactional emails: paper background, ink type, one solid CTA, no images (they are often blocked).
/// Table layout and inline styles for Gmail/Outlook; a prefers-color-scheme block for clients that honour it.
/// Every user-provided value is HTML-encoded; links are built by the server only.
/// </summary>
public static class EmailTemplates
{
    public static EmailMessage Welcome(string to, string name, string appUrl, string? verifyLink) => Build(to,
        subject: "ExamR-ə xoş gəldin",
        preheader: "Sual-cavab faylını yüklə — imtahan, öyrənmə və Battle hazırdır.",
        eyebrow: "Xoş gəldin",
        title: $"Salam, {name}.",
        paragraphs:
        [
            "ExamR hesabın hazırdır. Sual-cavab olan PDF və ya Word faylını yüklə — suallar avtomatik çıxarılır və dərhal istifadəyə hazır olur.",
        ],
        list:
        [
            ("Sınaq imtahanı", "vaxtlı, istədiyin sual aralığı ilə"),
            ("Öyrənmə rejimi", "cavabını yaz, ipucu al, müqayisə et"),
            ("Battle", "dostlarınla eyni sənəd üzrə canlı yarış"),
        ],
        cta: ("Öyrənməyə başla", appUrl),
        footnote: verifyLink is null ? null : ("Email ünvanını təsdiqlə", verifyLink, "Hesabını qorumaq üçün email ünvanını təsdiqlə. Link 24 saat etibarlıdır."));

    public static EmailMessage Verify(string to, string name, string link) => Build(to,
        subject: "ExamR — email ünvanını təsdiqlə",
        preheader: "Bir kliklə email ünvanını təsdiqlə. Link 24 saat etibarlıdır.",
        eyebrow: "Email təsdiqi",
        title: "Bu ünvan sənindir?",
        paragraphs:
        [
            $"Salam, {Name(name)}. ExamR hesabının email ünvanını təsdiqləmək üçün aşağıdakı düyməyə bas.",
            "Link 24 saat etibarlıdır və yalnız bir dəfə istifadə olunur.",
        ],
        list: [],
        cta: ("Ünvanı təsdiqlə", link),
        footnote: null,
        ignoreNote: "Bu hesabı sən yaratmamısansa, bu məktubu nəzərə alma — heç nə dəyişməyəcək.");

    public static EmailMessage PasswordReset(string to, string name, string link) => Build(to,
        subject: "ExamR — şifrəni yenilə",
        preheader: "Şifrə yeniləmə linki 30 dəqiqə etibarlıdır.",
        eyebrow: "Şifrənin yenilənməsi",
        title: "Yeni şifrə təyin et.",
        paragraphs:
        [
            $"Salam, {Name(name)}. Hesabın üçün şifrənin yenilənməsi tələb olundu.",
            "Link <strong>30 dəqiqə</strong> etibarlıdır və yalnız bir dəfə işləyir. Şifrə dəyişəndən sonra bütün açıq sessiyalar bağlanır.",
        ],
        list: [],
        cta: ("Yeni şifrə təyin et", link),
        footnote: null,
        ignoreNote: "Bu tələbi sən etməmisənsə, məktubu nəzərə alma — şifrən dəyişməyəcək.");

    public static EmailMessage SecurityNotice(string to, string message, string appUrl) => Build(to,
        "ExamR — hesab təhlükəsizliyi", "Hesabınızda təhlükəsizlik dəyişikliyi edildi.", "Təhlükəsizlik", "Şifrə yeniləndi",
        [message], [], ("Hesaba keç", appUrl), null);

    public static EmailMessage AccountDeletion(string to, string link) => Build(to,
        "ExamR — hesabın bağlanmasını təsdiqlə", "Link 30 dəqiqə etibarlıdır.", "Hesab", "Hesabı bağlamaq istəyirsiniz?",
        ["Bu linki yalnız hesabınızı bağlamaq istəyirsinizsə açın. Şəxsi profil anonimləşdiriləcək və giriş bağlanacaq. Ortaq sənədlər və tarixçə qorunacaq. Link 30 dəqiqə etibarlıdır."],
        [], ("Tələbi yoxla", link), null, "Bu tələbi siz etməmisinizsə, məktubu nəzərə almayın.");

    private static EmailMessage Build(string to, string subject, string preheader, string eyebrow, string title, string[] paragraphs,
        (string Term, string Text)[] list, (string Label, string Url) cta, (string Label, string Url, string Text)? footnote, string? ignoreNote = null)
    {
        static string E(string value) => WebUtility.HtmlEncode(value);
        // Paragraphs are server-written; the only dynamic piece inside them is the name, encoded by the callers below.
        var body = string.Concat(paragraphs.Select(p => $"""<p style="margin:0 0 14px;font-size:15px;line-height:24px;color:#3d4256">{p}</p>"""));
        var rows = string.Concat(list.Select(item => $"""
            <tr><td style="padding:10px 0;border-top:1px solid #e6e3db;font-size:14px;line-height:20px;color:#15172a"><strong>{E(item.Term)}</strong> <span style="color:#6a6f82">— {E(item.Text)}</span></td></tr>
            """));
        var listHtml = list.Length == 0 ? "" : $"""<table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="margin:6px 0 22px;border-bottom:1px solid #e6e3db">{rows}</table>""";
        var foot = footnote is { } f ? $"""
            <tr><td class="card" style="padding:20px 32px 26px;border-top:1px solid #e6e3db">
              <p style="margin:0 0 10px;font-size:13px;line-height:20px;color:#6a6f82">{E(f.Text)}</p>
              <a href="{E(f.Url)}" style="font-size:14px;font-weight:600;color:#15172a;text-decoration:underline">{E(f.Label)} →</a>
            </td></tr>
            """ : "";
        var ignore = ignoreNote is null ? "" : $"""<p style="margin:22px 0 0;font-size:13px;line-height:20px;color:#8d91a3">{E(ignoreNote)}</p>""";

        var html = $$"""
            <!doctype html>
            <html lang="az"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
            <meta name="color-scheme" content="light dark"><meta name="supported-color-schemes" content="light dark"><title>{{E(subject)}}</title>
            <style>
              @media (prefers-color-scheme: dark) {
                .bg { background:#0e1020 !important; } .card { background:#171a2c !important; border-color:#272b45 !important; }
                .ink, .card p, .card td { color:#eef0fb !important; } .btn { background:#eef0fb !important; } .btn a { color:#0e1020 !important; }
              }
              @media (max-width: 600px) { .card { padding-left:22px !important; padding-right:22px !important; } }
            </style></head>
            <body style="margin:0;padding:0;background:#fbfaf7">
            <div style="display:none;max-height:0;overflow:hidden;opacity:0">{{E(preheader)}}</div>
            <table role="presentation" class="bg" width="100%" cellpadding="0" cellspacing="0" style="background:#fbfaf7">
              <tr><td align="center" style="padding:32px 12px">
                <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="max-width:560px;font-family:-apple-system,'Segoe UI',Roboto,Helvetica,Arial,sans-serif">
                  <tr><td style="padding:0 4px 18px">
                    <table role="presentation" cellpadding="0" cellspacing="0"><tr>
                      <td style="width:26px;height:26px;background:#15172a;border-radius:7px;text-align:center;vertical-align:middle;font-size:13px;line-height:26px;color:#fbfaf7;font-weight:700">≡</td>
                      <td class="ink" style="padding-left:9px;font-size:17px;font-weight:700;letter-spacing:-0.3px;color:#15172a">ExamR</td>
                    </tr></table>
                  </td></tr>
                  <tr><td class="card" style="background:#ffffff;border:1px solid #e6e3db;border-radius:14px;padding:34px 32px 30px">
                    <p style="margin:0 0 12px;font-family:ui-monospace,Menlo,Consolas,monospace;font-size:11px;letter-spacing:1px;text-transform:uppercase;color:#5b45d6">{{E(eyebrow)}}</p>
                    <h1 class="ink" style="margin:0 0 18px;font-size:26px;line-height:32px;letter-spacing:-0.6px;color:#15172a">{{E(title)}}</h1>
                    {{body}}
                    {{listHtml}}
                    <table role="presentation" cellpadding="0" cellspacing="0" style="margin-top:8px"><tr>
                      <td class="btn" style="background:#15172a;border-radius:10px">
                        <a href="{{E(cta.Url)}}" style="display:inline-block;padding:13px 22px;font-size:15px;font-weight:600;color:#fbfaf7;text-decoration:none">{{E(cta.Label)}}</a>
                      </td></tr></table>
                    <p style="margin:18px 0 0;font-size:12px;line-height:18px;color:#8d91a3">Düymə işləmirsə, bu linki brauzerə kopyala:<br><span style="word-break:break-all;color:#6a6f82">{{E(cta.Url)}}</span></p>
                    {{ignore}}
                  </td></tr>
                  {{foot}}
                  <tr><td style="padding:18px 4px 0;font-size:12px;line-height:18px;color:#8d91a3">ExamR · sənədindən imtahana. Bu məktub hesabınla bağlı əməliyyat üçün avtomatik göndərilib.</td></tr>
                </table>
              </td></tr>
            </table>
            </body></html>
            """;

        var text = string.Join("\n\n", new[] { "ExamR", title }
            .Concat(paragraphs.Select(p => p.Replace("<strong>", "").Replace("</strong>", "")))
            .Concat(list.Select(i => $"- {i.Term}: {i.Text}"))
            .Append($"{cta.Label}: {cta.Url}")
            .Concat(footnote is { } n ? [$"{n.Text}\n{n.Label}: {n.Url}"] : [])
            .Concat(ignoreNote is null ? [] : [ignoreNote]));
        return new EmailMessage(to, subject, html, WebUtility.HtmlDecode(text));
    }

    /// <summary>Encode user-provided names before they go into the paragraph HTML.</summary>
    public static string Name(string? displayName) => WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(displayName) ? "ExamR istifadəçisi" : displayName.Trim());
}

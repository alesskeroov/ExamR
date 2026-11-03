using System.Net;

namespace ExamFlow.Api.Infrastructure.Email;

/// <summary>
/// ExamR transactional emails: paper background, ink type, one solid CTA, no images (they are often blocked).
/// Table layout and inline styles for Gmail/Outlook; a prefers-color-scheme block for clients that honour it.
/// Every user-provided value is HTML-encoded; links are built by the server only.
/// </summary>
public static class EmailTemplates
{
    /// <summary>Sent once, after the address is confirmed (or when a Google account is created). Carries no link secrets.</summary>
    public static EmailMessage Welcome(string to, string name, string appUrl) => Build(to,
        subject: "ExamR-ə xoş gəldiniz! 🎉",
        preheader: "Hesabınız hazırdır — sənədlərinizlə imtahana hazırlaşmağa başlayın.",
        eyebrow: "Xoş gəldiniz",
        title: $"Salam, {(string.IsNullOrWhiteSpace(name) ? "ExamR istifadəçisi" : name.Trim())}!", // Build encodes the title
        paragraphs:
        [
            "ExamR ailəsinə xoş gəldiniz! Hesabınız uğurla yaradıldı və e-mail ünvanınız təsdiqləndi.",
            "Artıq ExamR vasitəsilə:",
        ],
        list:
        [
            ("İmtahana hazırlıq", "öz sənədləriniz əsasında"),
            ("Bilik yoxlaması", "vaxtlı testlər və nəticələrin təhlili"),
            ("Battle", "dostlarınızla canlı yarışlar"),
            ("İnkişaf", "nəticələrinizi və irəliləyişinizi izləyin"),
        ],
        cta: ("ExamR-ə keçid et", appUrl),
        footnote: null,
        ignoreNote: "Öyrənməyə başlamağın vaxtıdır. Uğurlar arzulayırıq! — ExamR komandası");

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
        const string Ink = "#15172a", Muted = "#5f6478", Faint = "#8d91a3", Line = "#e8e9ef", Accent = "#4652c6", Page = "#f4f5f8";
        // Paragraphs are server-written; the only dynamic piece inside them is the name, encoded by the callers below.
        var body = string.Concat(paragraphs.Select(p => $"""<p class="muted" style="margin:0 0 14px;font-size:15px;line-height:24px;color:{Muted}">{p}</p>"""));
        // Feature rows: a tinted check badge (text, not SVG — Gmail strips SVG) next to a bold term and its description.
        var rows = string.Concat(list.Select(item => $"""
            <tr>
              <td width="34" valign="top" style="padding:0 0 14px">
                <div class="tint" style="width:24px;height:24px;border-radius:12px;background:#eef0fb;color:{Accent};font-size:13px;line-height:24px;text-align:center;font-weight:700">&#10003;</div>
              </td>
              <td valign="top" style="padding:2px 0 14px;font-size:14px;line-height:20px;color:{Ink}">
                <strong class="ink" style="color:{Ink}">{E(item.Term)}</strong><br><span class="muted" style="color:{Muted}">{E(item.Text)}</span>
              </td>
            </tr>
            """));
        var listHtml = list.Length == 0 ? "" : $"""<table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="margin:8px 0 18px">{rows}</table>""";
        var foot = footnote is { } f ? $"""
            <tr><td class="panel" style="padding:16px 18px;background:#f7f8fb;border-radius:10px">
              <p class="muted" style="margin:0 0 8px;font-size:13px;line-height:20px;color:{Muted}">{E(f.Text)}</p>
              <a href="{E(f.Url)}" style="font-size:14px;font-weight:600;color:{Accent};text-decoration:none">{E(f.Label)} &rarr;</a>
            </td></tr>
            <tr><td style="height:20px;line-height:20px">&nbsp;</td></tr>
            """ : "";
        var ignore = ignoreNote is null ? "" : $"""<p class="faint" style="margin:20px 0 0;font-size:13px;line-height:20px;color:{Faint}">{E(ignoreNote)}</p>""";
        var year = DateTime.UtcNow.Year;

        var html = $$"""
            <!doctype html>
            <html lang="az"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
            <meta name="color-scheme" content="light dark"><meta name="supported-color-schemes" content="light dark"><title>{{E(subject)}}</title>
            <style>
              body { -webkit-text-size-adjust:100%; }
              a.cta:hover { background:#3a45ab !important; }
              @media (prefers-color-scheme: dark) {
                body, .bg { background:#0e1020 !important; } .card { background:#171a2c !important; border-color:#272b45 !important; }
                .ink { color:#eef0fb !important; } .muted { color:#a3a9c2 !important; } .faint { color:#6f7592 !important; }
                .panel { background:#141730 !important; } .tint { background:#22273f !important; color:#a3adff !important; }
                .rule { border-color:#272b45 !important; } .mark { background:#eef0fb !important; color:#0e1020 !important; }
              }
              @media (max-width: 600px) {
                .card-pad { padding:28px 22px 24px !important; } .rule { padding-left:22px !important; padding-right:22px !important; }
                .cta { display:block !important; text-align:center !important; }
                h1 { font-size:22px !important; line-height:28px !important; }
              }
            </style></head>
            <body style="margin:0;padding:0;background:{{Page}}">
            <div style="display:none;max-height:0;overflow:hidden;opacity:0;mso-hide:all">{{E(preheader)}}&#847;&zwnj;&nbsp;&#847;&zwnj;&nbsp;&#847;&zwnj;&nbsp;</div>
            <table role="presentation" class="bg" width="100%" cellpadding="0" cellspacing="0" style="background:{{Page}}">
              <tr><td align="center" style="padding:36px 12px 28px">
                <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="max-width:560px;font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,Helvetica,Arial,sans-serif">
                  <tr><td style="padding:0 2px 20px">
                    <table role="presentation" cellpadding="0" cellspacing="0"><tr>
                      <td class="mark" style="width:30px;height:30px;background:{{Ink}};border-radius:8px;text-align:center;vertical-align:middle;font-size:15px;line-height:30px;color:#ffffff;font-weight:700">&#8801;</td>
                      <td class="ink" style="padding-left:10px;font-size:18px;font-weight:700;letter-spacing:-0.3px;color:{{Ink}}">ExamR</td>
                    </tr></table>
                  </td></tr>
                  <tr><td class="card" style="background:#ffffff;border:1px solid {{Line}};border-radius:16px;overflow:hidden">
                    <div style="height:4px;line-height:4px;font-size:0;background:{{Accent}}">&nbsp;</div>
                    <table role="presentation" width="100%" cellpadding="0" cellspacing="0"><tr><td class="card-pad" style="padding:36px 36px 30px">
                      <p style="margin:0 0 10px;font-size:12px;font-weight:600;letter-spacing:0.6px;text-transform:uppercase;color:{{Accent}}">{{E(eyebrow)}}</p>
                      <h1 class="ink" style="margin:0 0 16px;font-size:26px;line-height:33px;font-weight:700;letter-spacing:-0.5px;color:{{Ink}}">{{E(title)}}</h1>
                      {{body}}
                      {{listHtml}}
                      <table role="presentation" cellpadding="0" cellspacing="0" style="margin:10px 0 4px"><tr><td style="border-radius:10px;background:{{Accent}}">
                        <a class="cta" href="{{E(cta.Url)}}" style="display:inline-block;padding:14px 26px;font-size:15px;font-weight:600;line-height:20px;color:#ffffff;text-decoration:none;border-radius:10px;background:{{Accent}}">{{E(cta.Label)}} &rarr;</a>
                      </td></tr></table>
                      {{ignore}}
                    </td></tr></table>
                    <table role="presentation" width="100%" cellpadding="0" cellspacing="0"><tr><td class="rule" style="border-top:1px solid {{Line}};padding:16px 36px 20px">
                      <p class="faint" style="margin:0;font-size:12px;line-height:18px;color:{{Faint}}">Düymə işləmirsə, bu linki brauzerə kopyalayın:<br><a href="{{E(cta.Url)}}" style="word-break:break-all;color:{{Muted}};text-decoration:underline">{{E(cta.Url)}}</a></p>
                    </td></tr></table>
                  </td></tr>
                  <tr><td style="height:20px;line-height:20px">&nbsp;</td></tr>
                  {{foot}}
                  <tr><td align="center" style="padding:4px 12px 0">
                    <p class="faint" style="margin:0 0 6px;font-size:12px;line-height:18px;color:{{Faint}}"><strong class="muted" style="color:{{Muted}}">ExamR</strong> · sənədindən imtahana</p>
                    <p class="faint" style="margin:0;font-size:11px;line-height:17px;color:{{Faint}}">Bu məktub ExamR hesabınızla bağlı əməliyyat üçün avtomatik göndərilib. Cavab yazmağa ehtiyac yoxdur.<br>&copy; {{year}} ExamR</p>
                  </td></tr>
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

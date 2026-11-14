using System.Text;
using System.Xml.Linq;

namespace ExamFlow.Parsing;

/// <summary>
/// Office Math (OMML, used by Word and PowerPoint equations) as one line of plain text, so formulas survive indexing:
/// fractions become (a)/(b), short indices Unicode (H₂O, Na⁺) and longer ones x^(n+1) / a_(i,j), roots √(x),
/// n-ary operators ∑_(i=1)^n.
/// </summary>
public static class MathText
{
    public static readonly XNamespace M = "http://schemas.openxmlformats.org/officeDocument/2006/math";

    /// <summary>Linear text of an m:oMath or m:oMathPara element (or any element inside one).</summary>
    public static string Linear(XElement element) => Clean(Node(element));

    private static string Node(XElement e)
    {
        if (e.Name.Namespace != M) return Children(e);
        switch (e.Name.LocalName)
        {
            case "r": return string.Concat(e.Elements(M + "t").Select(t => t.Value));
            case "t": return e.Value;
            case "oMathPara": return string.Join("; ", e.Elements(M + "oMath").Select(Node));
            case "f":
            {
                var num = Arg(e, "num");
                var den = Arg(e, "den");
                return $"{Group(num)}/{Group(den)}";
            }
            case "sSup": return Arg(e, "e") + Super(Arg(e, "sup"));
            case "sSub": return Arg(e, "e") + Sub(Arg(e, "sub"));
            case "sSubSup": return Arg(e, "e") + Sub(Arg(e, "sub")) + Super(Arg(e, "sup"));
            case "sPre": return Sub(Arg(e, "sub")) + Super(Arg(e, "sup")) + Arg(e, "e");
            case "rad":
            {
                var degree = Arg(e, "deg");
                var body = $"√({Arg(e, "e")})";
                return degree.Length == 0 ? body : $"{Super(degree)}{body}";
            }
            case "d":
            {
                var props = e.Element(M + "dPr");
                var begin = Chr(props, "begChr", "(");
                var end = Chr(props, "endChr", ")");
                var separator = Chr(props, "sepChr", ",");
                return begin + string.Join(separator, e.Elements(M + "e").Select(Node)) + end;
            }
            case "nary":
            {
                var symbol = Chr(e.Element(M + "naryPr"), "chr", "∫");
                return symbol + Sub(Arg(e, "sub")) + Super(Arg(e, "sup")) + " " + Arg(e, "e");
            }
            case "func": return Arg(e, "fName") + Group(Arg(e, "e"));
            case "limLow": return Arg(e, "e") + Sub(Arg(e, "lim"));
            case "limUpp": return Arg(e, "e") + Super(Arg(e, "lim"));
            case "eqArr": return string.Join("; ", e.Elements(M + "e").Select(Node));
            case "m": return "[" + string.Join("; ", e.Elements(M + "mr").Select(r => string.Join(", ", r.Elements(M + "e").Select(Node)))) + "]";
            case "rPr": case "fPr": case "sSupPr": case "sSubPr": case "sSubSupPr": case "sPrePr": case "radPr": case "dPr": case "naryPr":
            case "funcPr": case "limLowPr": case "limUppPr": case "eqArrPr": case "mPr": case "accPr": case "barPr": case "boxPr":
            case "borderBoxPr": case "groupChrPr": case "ctrlPr": case "oMathParaPr":
                return "";
            default: return Children(e); // acc, bar, box, borderBox, groupChr, e, num, … : their content
        }
    }

    private static string Children(XElement e) => string.Concat(e.Elements().Select(Node));

    private static string Arg(XElement e, string name) => e.Element(M + name) is { } child ? Clean(Node(child)) : "";

    private static string Chr(XElement? props, string name, string fallback)
    {
        var attribute = props?.Element(M + name)?.Attribute(M + "val");
        // An empty value means "no character" (for example a bracket that is only opened).
        return attribute is null ? fallback : attribute.Value;
    }

    /// <summary>Parentheses around anything longer than one symbol, unless already bracketed.</summary>
    private static string Group(string text) =>
        text.Length <= 1 || (text[0] is '(' or '[' or '{' && text[^1] is ')' or ']' or '}') || IsSimple(text) ? text : $"({text})";

    private static bool IsSimple(string text) => text.All(char.IsLetterOrDigit) && (text.All(char.IsDigit) || text.Length == 1);

    private static string Super(string text) => text.Length == 0 ? "" : Script(text, SuperMap, out var mapped) ? mapped : "^" + Group(text);
    private static string Sub(string text) => text.Length == 0 ? "" : Script(text, SubMap, out var mapped) ? mapped : "_" + Group(text);

    /// <summary>Unicode indices only for short indices whose every character has one (digits, signs, n, i).</summary>
    private static bool Script(string text, IReadOnlyDictionary<char, char> map, out string mapped)
    {
        mapped = "";
        if (text.Length > 3) return false;
        var builder = new StringBuilder(text.Length);
        foreach (var c in text)
        {
            if (!map.TryGetValue(c, out var s)) return false;
            builder.Append(s);
        }
        mapped = builder.ToString();
        return true;
    }

    private static readonly Dictionary<char, char> SuperMap = new()
    {
        ['0'] = '⁰', ['1'] = '¹', ['2'] = '²', ['3'] = '³', ['4'] = '⁴', ['5'] = '⁵', ['6'] = '⁶', ['7'] = '⁷', ['8'] = '⁸', ['9'] = '⁹',
        ['+'] = '⁺', ['-'] = '⁻', ['−'] = '⁻', ['='] = '⁼', ['n'] = 'ⁿ', ['i'] = 'ⁱ',
    };

    private static readonly Dictionary<char, char> SubMap = new()
    {
        ['0'] = '₀', ['1'] = '₁', ['2'] = '₂', ['3'] = '₃', ['4'] = '₄', ['5'] = '₅', ['6'] = '₆', ['7'] = '₇', ['8'] = '₈', ['9'] = '₉',
        ['+'] = '₊', ['-'] = '₋', ['−'] = '₋', ['='] = '₌',
    };

    // U+2061–U+2064 (function application, invisible times/separator/plus) are invisible but break search and copying.
    private static string Clean(string text) =>
        string.Join(' ', text.Replace("⁡", "").Replace("⁢", "").Replace("⁣", "").Replace("⁤", "")
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}

using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace StajEvrak.Core;

/// <summary>Türkçe metin normalleştirme ve değer dönüştürme yardımcıları.</summary>
public static class TextUtils
{
    private static readonly CultureInfo Tr = new("tr-TR");

    private static readonly Dictionary<char, char> AsciiFold = new()
    {
        ['ı'] = 'i', ['ğ'] = 'g', ['ü'] = 'u', ['ş'] = 's', ['ö'] = 'o',
        ['ç'] = 'c', ['â'] = 'a', ['î'] = 'i', ['û'] = 'u'
    };

    private static readonly string[] DateFormats =
    {
        "yyyy-MM-dd", "dd.MM.yyyy", "d.M.yyyy", "dd/MM/yyyy", "d/M/yyyy",
        "dd-MM-yyyy", "d-M-yyyy", "dd.MM.yy", "yyyy/MM/dd"
    };

    public static string TrLower(string text) => (text ?? "").ToLower(Tr);

    /// <summary>Karşılaştırma için: küçük harf, Türkçe karakter sadeleştirme, noktalama/boşluk temizliği.</summary>
    public static string Fold(string text)
    {
        var lower = TrLower(text);
        var sb = new StringBuilder(lower.Length);
        foreach (var ch in lower)
            sb.Append(AsciiFold.TryGetValue(ch, out var r) ? r : ch);
        var t = Regex.Replace(sb.ToString(), @"[^\w@+]+", " ");
        return Regex.Replace(t, @"\s+", " ").Trim();
    }

    public static string DigitsOnly(string text) => Regex.Replace(text ?? "", @"\D", "");

    public static string NormalizePhone(string text)
    {
        var d = DigitsOnly(text);
        if (d.StartsWith("90") && d.Length == 12) d = d[2..];
        if (d.StartsWith("0") && d.Length == 11) d = d[1..];
        return d;
    }

    public static DateTime? ParseDate(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var text = value.Trim().Split(' ', 'T')[0];
        if (DateTime.TryParseExact(text, DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
            return d.Date;
        return null;
    }

    public static string FormatDateTr(string value)
    {
        var d = ParseDate(value);
        return d.HasValue ? d.Value.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture) : (value ?? "");
    }

    public static string CollapseSpaces(string text) => Regex.Replace(text ?? "", @"\s+", " ").Trim();

    public static string NowString() => Format(DateTime.Now);

    public static string Format(DateTime value) => value.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

    public static DateTime? ParseDateTime(string value) =>
        DateTime.TryParseExact(value ?? "", "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
            ? d : null;
}

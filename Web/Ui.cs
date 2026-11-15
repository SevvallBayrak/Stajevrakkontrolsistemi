using System.Net;
using System.Text;
using Microsoft.AspNetCore.Html;
using StajEvrak.Core;

namespace StajEvrak.Web;

/// <summary>Görünümlerde kullanılan küçük HTML yardımcıları (ikon, statü rozeti, akış çubuğu).</summary>
public static class Ui
{
    private static readonly Dictionary<string, string> IconPaths = new()
    {
        ["grid"] = "<rect x=\"3\" y=\"3\" width=\"7\" height=\"7\" rx=\"1.5\"/><rect x=\"14\" y=\"3\" width=\"7\" height=\"7\" rx=\"1.5\"/><rect x=\"3\" y=\"14\" width=\"7\" height=\"7\" rx=\"1.5\"/><rect x=\"14\" y=\"14\" width=\"7\" height=\"7\" rx=\"1.5\"/>",
        ["plus"] = "<path d=\"M12 5v14M5 12h14\"/>",
        ["user"] = "<circle cx=\"12\" cy=\"8\" r=\"4\"/><path d=\"M4 21c0-4 4-6 8-6s8 2 8 6\"/>",
        ["users"] = "<circle cx=\"9\" cy=\"8\" r=\"3.5\"/><path d=\"M2 20c0-3.5 3-5.5 7-5.5s7 2 7 5.5\"/><path d=\"M16 4.5a3.5 3.5 0 0 1 0 7M18 14.6c2.4.6 4 2.4 4 5.4\"/>",
        ["file"] = "<path d=\"M14 3H7a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h10a2 2 0 0 0 2-2V8z\"/><path d=\"M14 3v5h5M9 13h6M9 17h4\"/>",
        ["check"] = "<path d=\"M20 6 9 17l-5-5\"/>",
        ["check-circle"] = "<circle cx=\"12\" cy=\"12\" r=\"9\"/><path d=\"m8 12 3 3 5-6\"/>",
        ["x-circle"] = "<circle cx=\"12\" cy=\"12\" r=\"9\"/><path d=\"m15 9-6 6M9 9l6 6\"/>",
        ["database"] = "<ellipse cx=\"12\" cy=\"5\" rx=\"8\" ry=\"3\"/><path d=\"M4 5v6c0 1.7 3.6 3 8 3s8-1.3 8-3V5M4 11v6c0 1.7 3.6 3 8 3s8-1.3 8-3v-6\"/>",
        ["shield"] = "<path d=\"M12 3 4 6v6c0 5 3.5 8 8 9 4.5-1 8-4 8-9V6z\"/><path d=\"m9 12 2 2 4-4\"/>",
        ["upload"] = "<path d=\"M12 16V4M7 9l5-5 5 5\"/><path d=\"M4 16v3a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2v-3\"/>",
        ["download"] = "<path d=\"M12 4v12M7 11l5 5 5-5\"/><path d=\"M4 18v1a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2v-1\"/>",
        ["logout"] = "<path d=\"M15 4h3a2 2 0 0 1 2 2v12a2 2 0 0 1-2 2h-3M10 17l5-5-5-5M15 12H3\"/>",
        ["alert"] = "<path d=\"M12 3 2 20h20z\"/><path d=\"M12 10v4M12 17.5v.01\"/>",
        ["info"] = "<circle cx=\"12\" cy=\"12\" r=\"9\"/><path d=\"M12 11v5M12 7.5v.01\"/>",
        ["trash"] = "<path d=\"M4 7h16M10 11v6M14 11v6M6 7l1 12a2 2 0 0 0 2 2h6a2 2 0 0 0 2-2l1-12M9 7V4h6v3\"/>",
        ["edit"] = "<path d=\"M4 20h4L19 9l-4-4L4 16z\"/><path d=\"m13.5 6.5 4 4\"/>",
        ["refresh"] = "<path d=\"M20 11a8 8 0 0 0-14.5-4.5L4 8M4 4v4h4M4 13a8 8 0 0 0 14.5 4.5L20 16M20 20v-4h-4\"/>",
        ["calendar"] = "<rect x=\"3\" y=\"5\" width=\"18\" height=\"16\" rx=\"2\"/><path d=\"M3 10h18M8 3v4M16 3v4\"/>",
        ["link"] = "<path d=\"M10 14a4 4 0 0 0 5.7 0l3-3a4 4 0 0 0-5.7-5.7l-1 1\"/><path d=\"M14 10a4 4 0 0 0-5.7 0l-3 3a4 4 0 0 0 5.7 5.7l1-1\"/>",
        ["lock"] = "<rect x=\"4\" y=\"10\" width=\"16\" height=\"11\" rx=\"2\"/><path d=\"M8 10V7a4 4 0 0 1 8 0v3\"/>",
        ["unlock"] = "<rect x=\"4\" y=\"10\" width=\"16\" height=\"11\" rx=\"2\"/><path d=\"M8 10V7a4 4 0 0 1 7.5-2\"/>",
        ["key"] = "<circle cx=\"8\" cy=\"15\" r=\"4\"/><path d=\"m11 12 9-9M17 6l3 3M15 8l2 2\"/>",
        ["eye"] = "<path d=\"M2 12s3.5-7 10-7 10 7 10 7-3.5 7-10 7S2 12 2 12z\"/><circle cx=\"12\" cy=\"12\" r=\"3\"/>",
        ["search"] = "<circle cx=\"11\" cy=\"11\" r=\"7\"/><path d=\"m20 20-4-4\"/>",
        ["arrow-right"] = "<path d=\"M5 12h14M13 6l6 6-6 6\"/>",
        ["arrow-left"] = "<path d=\"M19 12H5M11 6l-6 6 6 6\"/>",
        ["scan"] = "<path d=\"M4 8V5a1 1 0 0 1 1-1h3M16 4h3a1 1 0 0 1 1 1v3M20 16v3a1 1 0 0 1-1 1h-3M8 20H5a1 1 0 0 1-1-1v-3M4 12h16\"/>",
        ["building"] = "<rect x=\"4\" y=\"3\" width=\"16\" height=\"18\" rx=\"1\"/><path d=\"M9 21v-4h6v4M8 7h2M14 7h2M8 11h2M14 11h2\"/>",
        ["id"] = "<rect x=\"3\" y=\"5\" width=\"18\" height=\"14\" rx=\"2\"/><circle cx=\"9\" cy=\"11\" r=\"2.5\"/><path d=\"M5.5 16c.5-1.5 2-2.5 3.5-2.5s3 1 3.5 2.5M15 10h3M15 13h3\"/>",
        ["layers"] = "<path d=\"m12 3 9 5-9 5-9-5z\"/><path d=\"m3 13 9 5 9-5\"/>",
        ["sparkle"] = "<path d=\"M12 3v4M12 17v4M3 12h4M17 12h4M6 6l2.5 2.5M15.5 15.5 18 18M6 18l2.5-2.5M15.5 8.5 18 6\"/>",
    };

    public static IHtmlContent Icon(string name, string cls = "") =>
        new HtmlString($"<svg class=\"svg-icon {cls}\" viewBox=\"0 0 24 24\" aria-hidden=\"true\">{(IconPaths.TryGetValue(name, out var p) ? p : "")}</svg>");

    public static IHtmlContent Badge(DocumentStatus status, bool large = false) =>
        new HtmlString($"<span class=\"badge {status.Css()}{(large ? " lg" : "")}\">{WebUtility.HtmlEncode(status.Label())}</span>");

    /// <summary>Statü akış çubuğu: Yüklendi → İnceleniyor → Hatalı/Eksik → Onaylandı → Tamamlandı</summary>
    public static IHtmlContent StatusFlow(DocumentStatus status)
    {
        var order = new[] { DocumentStatus.YUKLENDI, DocumentStatus.INCELENIYOR, DocumentStatus.HATALI_EKSIK,
                            DocumentStatus.ONAYLANDI, DocumentStatus.TAMAMLANDI };
        var cur = Array.IndexOf(order, status);
        var sb = new StringBuilder("<div class=\"flow\">");
        for (var i = 0; i < order.Length; i++)
        {
            var s = order[i];
            string cls;
            if (s == DocumentStatus.HATALI_EKSIK && (status == DocumentStatus.ONAYLANDI || status == DocumentStatus.TAMAMLANDI)) cls = "done";
            else if (i < cur) cls = "done";
            else if (i == cur) cls = s == DocumentStatus.HATALI_EKSIK ? "bad" : "now";
            else cls = "";
            var mark = cls == "done" ? "✓" : (i + 1).ToString();
            sb.Append($"<div class=\"step {cls}\"><span class=\"b\">{mark}</span>{WebUtility.HtmlEncode(s.Label())}</div>");
            if (i < order.Length - 1) sb.Append($"<div class=\"bar {(i < cur ? "done" : "")}\"></div>");
        }
        sb.Append("</div>");
        return new HtmlString(sb.ToString());
    }

    public static IHtmlContent StatusIcon(string itemStatus) => itemStatus switch
    {
        ItemStatus.Match => Icon("check-circle"),
        ItemStatus.Mismatch or ItemStatus.Missing or ItemStatus.NotEntered => Icon("x-circle"),
        _ => Icon("info")
    };

    public static IHtmlContent FlashIcon(string category) => Icon(category switch
    {
        "success" => "check-circle",
        "error" => "x-circle",
        "locked" => "lock",
        "warning" => "alert",
        _ => "info"
    });

    public static string Date(string value) => TextUtils.FormatDateTr(value);

    public static string Short(string dateTime) =>
        string.IsNullOrEmpty(dateTime) ? "" : (dateTime.Length > 16 ? dateTime[..16] : dateTime);

    public static string OrDash(string value) => string.IsNullOrWhiteSpace(value) ? "—" : value;
}

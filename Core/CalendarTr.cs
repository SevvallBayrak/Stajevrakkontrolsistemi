using System.Globalization;

namespace StajEvrak.Core;

/// <summary>
/// Staj günü hesaplama yardımcısı (Bölüm 6.3 için altyapı).
/// Seçilen haftalık günler ve tarih aralığına göre staj günlerini sayar; resmi tatilleri çıkarır.
/// Dini bayram tarihleri her yıl değiştiğinden RELIGIOUS tablosu yıllık güncellenmelidir
/// (yarım gün arifeler sayılmaz).
/// </summary>
public sealed class TurkishHolidayCalendar
{
    private static readonly (int Month, int Day, string Name)[] Fixed =
    {
        (1, 1, "Yılbaşı"),
        (4, 23, "Ulusal Egemenlik ve Çocuk Bayramı"),
        (5, 1, "Emek ve Dayanışma Günü"),
        (5, 19, "Atatürk'ü Anma, Gençlik ve Spor Bayramı"),
        (7, 15, "Demokrasi ve Millî Birlik Günü"),
        (8, 30, "Zafer Bayramı"),
        (10, 29, "Cumhuriyet Bayramı"),
    };

    private static readonly Dictionary<int, (string First, int Length, string Name)[]> Religious = new()
    {
        [2025] = new[] { ("2025-03-30", 3, "Ramazan Bayramı"), ("2025-06-06", 4, "Kurban Bayramı") },
        [2026] = new[] { ("2026-03-20", 3, "Ramazan Bayramı"), ("2026-05-27", 4, "Kurban Bayramı") },
        [2027] = new[] { ("2027-03-09", 3, "Ramazan Bayramı"), ("2027-05-16", 4, "Kurban Bayramı") },
    };

    public Dictionary<DateTime, string> HolidaysBetween(DateTime start, DateTime end)
    {
        var result = new Dictionary<DateTime, string>();
        for (var year = start.Year; year <= end.Year; year++)
        {
            foreach (var (m, d, name) in Fixed) result[new DateTime(year, m, d)] = name;
            if (!Religious.TryGetValue(year, out var list)) continue;
            foreach (var (first, length, name) in list)
            {
                var day = DateTime.ParseExact(first, "yyyy-MM-dd", CultureInfo.InvariantCulture);
                for (var i = 0; i < length; i++) result[day.AddDays(i)] = name;
            }
        }
        return result.Where(kv => kv.Key >= start && kv.Key <= end).ToDictionary(kv => kv.Key, kv => kv.Value);
    }

    /// <summary>weekdays: 0 = Pazartesi ... 5 = Cumartesi</summary>
    public InternshipDayResult CountInternshipDays(DateTime start, DateTime end, IEnumerable<int> weekdays)
    {
        var selected = new HashSet<int>(weekdays);
        var holidays = HolidaysBetween(start, end);
        var days = new List<DateTime>();
        var skipped = new List<SkippedHoliday>();
        for (var current = start.Date; current <= end.Date; current = current.AddDays(1))
        {
            var mondayBased = ((int)current.DayOfWeek + 6) % 7; // Pazartesi = 0
            if (!selected.Contains(mondayBased)) continue;
            if (holidays.TryGetValue(current, out var name))
                skipped.Add(new SkippedHoliday { Date = current.ToString("dd.MM.yyyy"), Name = name });
            else
                days.Add(current);
        }
        return new InternshipDayResult
        {
            Ok = true,
            Count = days.Count,
            SkippedHolidays = skipped,
            First = days.Count > 0 ? days[0].ToString("dd.MM.yyyy") : null,
            Last = days.Count > 0 ? days[^1].ToString("dd.MM.yyyy") : null,
        };
    }
}

public sealed class SkippedHoliday
{
    public string Date { get; set; }
    public string Name { get; set; }
}

public sealed class InternshipDayResult
{
    public bool Ok { get; set; }
    public string Message { get; set; }
    public int Count { get; set; }
    public List<SkippedHoliday> SkippedHolidays { get; set; } = new();
    public string First { get; set; }
    public string Last { get; set; }
}

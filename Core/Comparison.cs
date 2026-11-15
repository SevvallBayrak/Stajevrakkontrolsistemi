namespace StajEvrak.Core;

/*
 * Belgeden okunan bilgiler ile kullanıcının sisteme girdiği bilgilerin karşılaştırılması.
 *  - Soru-cevap #1: belgeden elde edilen bilgiler ile kullanıcının girdiği bilgiler karşılaştırılarak
 *    belgenin teslime hazır olup olmadığı kontrol edilir.
 *  - Bölüm 4.3: Ek-16'daki öğrenci bilgileri veri setiyle karşılaştırılır, uyuşmazlık raporda belirtilir.
 */

// ---------------------------------------------------------------------------
// Değer karşılaştırıcılar (strateji deseni + çok biçimlilik)
// ---------------------------------------------------------------------------
public abstract class ValueComparer
{
    public abstract string Normalize(string value);
    public bool AreEqual(string a, string b) => Normalize(a) == Normalize(b);
    public virtual string Display(string value) => value ?? "";
}

public sealed class TextComparer : ValueComparer
{
    public override string Normalize(string value) => TextUtils.Fold(value ?? "").Replace(" ", "");
}

public sealed class DigitsComparer : ValueComparer
{
    public override string Normalize(string value) => TextUtils.DigitsOnly(value);
}

public sealed class PhoneComparer : ValueComparer
{
    public override string Normalize(string value) => TextUtils.NormalizePhone(value);
}

public sealed class EmailComparer : ValueComparer
{
    public override string Normalize(string value) => (value ?? "").Trim().ToLowerInvariant();
}

public sealed class DateComparer : ValueComparer
{
    public override string Normalize(string value)
    {
        var d = TextUtils.ParseDate(value);
        return d.HasValue ? d.Value.ToString("yyyy-MM-dd") : (value ?? "").Trim();
    }

    public override string Display(string value) => TextUtils.FormatDateTr(value);
}

public sealed class IntComparer : ValueComparer
{
    public override string Normalize(string value)
    {
        var d = TextUtils.DigitsOnly(value);
        return d.Length == 0 ? "" : (long.TryParse(d, out var n) ? n.ToString() : d);
    }
}

public static class Comparers
{
    private static readonly Dictionary<string, ValueComparer> ByKind = new()
    {
        ["text"] = new TextComparer(),
        ["digits"] = new DigitsComparer(),
        ["phone"] = new PhoneComparer(),
        ["email"] = new EmailComparer(),
        ["date"] = new DateComparer(),
        ["int"] = new IntComparer(),
    };

    public static ValueComparer For(Ek16Field field) =>
        ByKind.TryGetValue(field.Kind, out var c) ? c : ByKind["text"];
}

// ---------------------------------------------------------------------------
// Rapor
// ---------------------------------------------------------------------------
public static class ItemStatus
{
    public const string Match = "match";
    public const string Mismatch = "mismatch";
    public const string Missing = "missing";
    public const string NotEntered = "not_entered";
    public const string OptionalEmpty = "optional_empty";
    public const string Unread = "unread";

    public static readonly Dictionary<string, string> Labels = new()
    {
        [Match] = "Uyumlu",
        [Mismatch] = "Uyuşmuyor",
        [Missing] = "Belgede boş / bulunamadı",
        [NotEntered] = "Sisteme girilmemiş",
        [OptionalEmpty] = "İsteğe bağlı (boş)",
        [Unread] = "Okunamadı",
    };
}

public class ReportItem
{
    public string Key { get; set; }
    public string Label { get; set; }
    public string Group { get; set; }
    public string Status { get; set; }
    public string StatusLabel { get; set; }
    public string Entered { get; set; }
    public string DocumentValue { get; set; }
}

public class DatasetItem
{
    public string Key { get; set; }
    public string Label { get; set; }
    public string Status { get; set; }
    public string StatusLabel { get; set; }
    public string Value { get; set; }
    public string DatasetValue { get; set; }
}

public class ComparisonReport
{
    public string GeneratedAt { get; set; }
    public string Source { get; set; }
    public bool Readable { get; set; }
    public int ImageCount { get; set; }
    public List<ReportItem> Items { get; set; } = new();
    public List<DatasetItem> DatasetItems { get; set; } = new();
    public bool DatasetFound { get; set; }
    public Dictionary<string, int> Counts { get; set; } = new();
    public int DatasetMismatch { get; set; }
    public bool Ready { get; set; }
    public bool ManualReview { get; set; }
    public List<string> Messages { get; set; } = new();

    public int Count(string status) => Counts != null && Counts.TryGetValue(status, out var n) ? n : 0;
    public int ProblemCount => Count(ItemStatus.Mismatch) + Count(ItemStatus.Missing) + Count(ItemStatus.NotEntered);
}

public sealed class DocumentComparator
{
    // Veri seti karşılaştırmasında: belgedeki alan → veri seti sütunu
    private static readonly (string DocKey, string DatasetKey)[] DatasetMap =
    {
        ("ad_soyad", "ad_soyad"), ("tc_kimlik_no", "tc_kimlik_no"), ("uyruk", "uyruk"),
        ("ogrenci_no", "ogrenci_no"), ("dogum_tarihi", "dogum_tarihi"), ("bolum", "bolum"),
        ("ikametgah_adresi", "ikametgah_adresi"), ("eposta", "eposta"), ("telefon", "telefon"),
        ("saglik_yardimi", "saglik_yardimi"), ("ogretim_yili", "teslim_yili"),
    };

    public ComparisonReport Compare(Dictionary<string, string> formData, ParsedDocument parsed,
                                    Dictionary<string, string> extracted, DatasetRecord dataset)
    {
        var report = new ComparisonReport
        {
            GeneratedAt = DateTime.Now.ToString("dd.MM.yyyy HH:mm"),
            Source = parsed.Source,
            Readable = parsed.Readable,
            ImageCount = parsed.ImageCount,
            DatasetFound = dataset != null,
        };

        foreach (var f in Ek16Schema.Fields)
        {
            var cmp = Comparers.For(f);
            var entered = formData.TryGetValue(f.Key, out var e) ? e ?? "" : "";
            var docVal = extracted.TryGetValue(f.Key, out var d) ? d ?? "" : "";
            string status;
            if (!parsed.Readable) status = ItemStatus.Unread;
            else if (cmp.Normalize(docVal).Length == 0) status = f.Required ? ItemStatus.Missing : ItemStatus.OptionalEmpty;
            else if (cmp.Normalize(entered).Length == 0) status = f.Required ? ItemStatus.NotEntered : ItemStatus.Match;
            else status = cmp.AreEqual(entered, docVal) ? ItemStatus.Match : ItemStatus.Mismatch;

            report.Items.Add(new ReportItem
            {
                Key = f.Key, Label = f.Label, Group = f.Group, Status = status,
                StatusLabel = ItemStatus.Labels[status], Entered = cmp.Display(entered), DocumentValue = cmp.Display(docVal),
            });
        }

        report.DatasetItems = CompareDataset(formData, extracted, dataset, parsed.Readable);
        foreach (var key in ItemStatus.Labels.Keys)
            report.Counts[key] = report.Items.Count(i => i.Status == key);
        report.DatasetMismatch = report.DatasetItems.Count(i => i.Status == ItemStatus.Mismatch);
        report.ManualReview = !parsed.Readable;
        report.Ready = parsed.Readable && report.ProblemCount == 0;

        if (!string.IsNullOrEmpty(parsed.Note)) report.Messages.Add(parsed.Note);
        if (report.Count(ItemStatus.Mismatch) > 0)
            report.Messages.Add($"{report.Count(ItemStatus.Mismatch)} alanda belgedeki değer ile sisteme girilen değer uyuşmuyor.");
        if (report.Count(ItemStatus.Missing) > 0)
            report.Messages.Add($"{report.Count(ItemStatus.Missing)} zorunlu alan belgede boş veya okunamadı.");
        if (report.Count(ItemStatus.NotEntered) > 0)
            report.Messages.Add($"{report.Count(ItemStatus.NotEntered)} alan belgede dolu ancak sisteme girilmemiş.");
        if (dataset == null)
            report.Messages.Add("Öğrenci, admin tarafından yüklenen veri setinde bulunamadı.");
        else if (report.DatasetMismatch > 0)
            report.Messages.Add($"Öğrenci bilgileri veri setiyle {report.DatasetMismatch} alanda uyuşmuyor.");
        if (report.Ready)
            report.Messages.Insert(0, "Belgedeki bilgiler sisteme girilen bilgilerle uyumlu. Belge teslime hazır.");
        return report;
    }

    private static List<DatasetItem> CompareDataset(Dictionary<string, string> formData, Dictionary<string, string> extracted,
                                                    DatasetRecord dataset, bool readable)
    {
        var items = new List<DatasetItem>();
        if (dataset == null) return items;
        foreach (var (docKey, dsKey) in DatasetMap)
        {
            var f = Ek16Schema.FieldsByKey[docKey];
            var cmp = Comparers.For(f);
            var value = readable && extracted.TryGetValue(docKey, out var ex) && !string.IsNullOrEmpty(ex)
                ? ex
                : (formData.TryGetValue(docKey, out var fv) ? fv : "");
            var dsVal = dataset.Get(dsKey);
            string status;
            if (cmp.Normalize(dsVal).Length == 0) status = ItemStatus.OptionalEmpty;
            else if (cmp.Normalize(value).Length == 0) status = ItemStatus.Missing;
            else status = cmp.AreEqual(value, dsVal) ? ItemStatus.Match : ItemStatus.Mismatch;

            items.Add(new DatasetItem
            {
                Key = docKey,
                Label = Ek16Schema.DatasetLabels.TryGetValue(dsKey, out var l) ? l : f.Label,
                Status = status,
                StatusLabel = ItemStatus.Labels[status],
                Value = cmp.Display(value),
                DatasetValue = cmp.Display(dsVal),
            });
        }
        return items;
    }
}

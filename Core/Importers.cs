using System.Globalization;
using System.Text;
using DocumentFormat.OpenXml.Packaging;
using S = DocumentFormat.OpenXml.Spreadsheet;

namespace StajEvrak.Core;

/*
 * Admin test veri seti içe aktarma (Bölüm 4.3).
 * DatasetImporter soyut sınıfı satırları okuma işini alt sınıflara bırakır
 * (CsvDatasetImporter, XlsxDatasetImporter); başlık eşleme ve satır doğrulama
 * ortak olarak üst sınıfta yapılır (kalıtım).
 */

public sealed class ImportResult
{
    public List<DatasetRecord> Records { get; } = new();
    public List<string> Errors { get; } = new();
    public List<string> Warnings { get; } = new();
    public Dictionary<string, string> ColumnMap { get; } = new();
    public int TotalRows { get; set; }
    public bool MissingRequiredColumns { get; set; }

    public bool Ok => Records.Count > 0 && !MissingRequiredColumns;
}

public abstract class DatasetImporter
{
    /// <summary>Başlık satırı dahil tüm satırları döndürür.</summary>
    protected abstract List<List<string>> ReadRows(byte[] content);

    public ImportResult Import(byte[] content)
    {
        var result = new ImportResult();
        List<List<string>> rows;
        try
        {
            rows = ReadRows(content).Where(r => r.Any(c => !string.IsNullOrWhiteSpace(c))).ToList();
        }
        catch (Exception ex)
        {
            result.Errors.Add($"Dosya okunamadı: {ex.Message}");
            return result;
        }
        if (rows.Count < 2)
        {
            result.Errors.Add("Dosyada başlık satırı ve en az bir veri satırı bulunmalıdır.");
            return result;
        }

        var header = rows[0].Select(h => (h ?? "").Trim()).ToList();
        var mapping = MapHeader(header);
        foreach (var (key, idx) in mapping)
            result.ColumnMap[Ek16Schema.DatasetLabels[key]] = header[idx];

        var missing = Ek16Schema.DatasetRequired.Where(k => !mapping.ContainsKey(k)).Select(k => Ek16Schema.DatasetLabels[k]).ToList();
        if (missing.Count > 0)
        {
            result.MissingRequiredColumns = true;
            result.Errors.Add("Zorunlu sütun(lar) bulunamadı: " + string.Join(", ", missing));
            return result;
        }
        foreach (var k in Ek16Schema.DatasetKeys.Where(k => !mapping.ContainsKey(k)))
            result.Warnings.Add($"'{Ek16Schema.DatasetLabels[k]}' sütunu dosyada yok, boş bırakıldı.");

        var seen = new HashSet<string>();
        for (var i = 1; i < rows.Count; i++)
        {
            var lineNo = i + 1;
            result.TotalRows++;
            var values = mapping.ToDictionary(m => m.Key, m => m.Value < rows[i].Count ? rows[i][m.Value] : null);
            var (record, error) = BuildRecord(values);
            if (error != null)
            {
                result.Errors.Add($"Satır {lineNo}: {error}");
                continue;
            }
            if (seen.Contains(record.TcKimlikNo) || seen.Contains(record.OgrenciNo))
            {
                result.Errors.Add($"Satır {lineNo}: aynı TC / öğrenci no dosyada tekrar ediyor, atlandı.");
                continue;
            }
            seen.Add(record.TcKimlikNo);
            seen.Add(record.OgrenciNo);
            result.Records.Add(record);
        }
        return result;
    }

    // --- Ortak yardımcılar
    private static Dictionary<string, int> MapHeader(List<string> header)
    {
        var mapping = new Dictionary<string, int>();
        var folded = header.Select(TextUtils.Fold).ToList();
        foreach (var (key, aliases) in Ek16Schema.DatasetColumns)
        {
            foreach (var alias in new[] { key }.Concat(aliases))
            {
                var idx = folded.IndexOf(TextUtils.Fold(alias));
                if (idx >= 0 && !mapping.ContainsValue(idx))
                {
                    mapping[key] = idx;
                    break;
                }
            }
        }
        return mapping;
    }

    private static string Text(Dictionary<string, string> v, string key)
    {
        if (!v.TryGetValue(key, out var raw) || raw == null) return null;
        var t = raw.Trim();
        return t.Length == 0 ? null : t;
    }

    private static string NormalizeDate(string raw)
    {
        if (raw == null) return null;
        var d = TextUtils.ParseDate(raw);
        // Excel tarihleri sayı (OADate) olarak saklanabilir
        if (!d.HasValue && double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var oa) && oa > 1000 && oa < 80000)
            d = DateTime.FromOADate(oa).Date;
        return d.HasValue ? d.Value.ToString("yyyy-MM-dd") : raw;
    }

    private static (DatasetRecord, string) BuildRecord(Dictionary<string, string> v)
    {
        var ad = Text(v, "ad_soyad");
        var tcRaw = Text(v, "tc_kimlik_no");
        var tc = TextUtils.DigitsOnly(tcRaw);
        var no = TextUtils.DigitsOnly(Text(v, "ogrenci_no"));
        if (ad == null) return (null, "Ad soyad boş.");
        if (tc.Length != 11) return (null, $"TC kimlik no 11 haneli olmalı ('{tcRaw}').");
        if (no.Length == 0) return (null, "Öğrenci no boş.");
        var staj = TextUtils.DigitsOnly(Text(v, "onceki_staj_gun"));
        var email = Text(v, "eposta");
        return (new DatasetRecord
        {
            AdSoyad = TextUtils.CollapseSpaces(ad),
            TcKimlikNo = tc,
            OgrenciNo = no,
            Uyruk = Text(v, "uyruk"),
            DogumTarihi = NormalizeDate(Text(v, "dogum_tarihi")),
            IkametgahAdresi = Text(v, "ikametgah_adresi"),
            Bolum = Text(v, "bolum"),
            TeslimYili = Text(v, "teslim_yili"),
            OncekiStajGun = staj.Length > 0 && int.TryParse(staj, out var n) ? n : null,
            Eposta = email?.ToLowerInvariant(),
            Telefon = Text(v, "telefon"),
            SaglikYardimi = Text(v, "saglik_yardimi"),
        }, null);
    }
}

public sealed class CsvDatasetImporter : DatasetImporter
{
    protected override List<List<string>> ReadRows(byte[] content)
    {
        string text;
        try
        {
            text = new UTF8Encoding(false, true).GetString(content);
        }
        catch (DecoderFallbackException)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            text = Encoding.GetEncoding(1254).GetString(content); // Türkçe Windows (Excel CSV)
        }
        text = text.TrimStart('﻿');

        var firstLine = text.Split('\n')[0];
        var delimiter = new[] { ';', ',', '\t' }.OrderByDescending(d => firstLine.Count(c => c == d)).First();
        return Parse(text, delimiter);
    }

    /// <summary>Tırnaklı alanları destekleyen basit CSV ayrıştırıcı (RFC 4180).</summary>
    private static List<List<string>> Parse(string text, char delimiter)
    {
        var rows = new List<List<string>>();
        var row = new List<string>();
        var field = new StringBuilder();
        var inQuotes = false;
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (inQuotes)
            {
                if (c == '"' && i + 1 < text.Length && text[i + 1] == '"') { field.Append('"'); i++; }
                else if (c == '"') inQuotes = false;
                else field.Append(c);
            }
            else if (c == '"') inQuotes = true;
            else if (c == delimiter) { row.Add(field.ToString()); field.Clear(); }
            else if (c == '\n')
            {
                row.Add(field.ToString().TrimEnd('\r'));
                field.Clear();
                rows.Add(row);
                row = new List<string>();
            }
            else field.Append(c);
        }
        if (field.Length > 0 || row.Count > 0)
        {
            row.Add(field.ToString().TrimEnd('\r'));
            rows.Add(row);
        }
        return rows;
    }
}

public sealed class XlsxDatasetImporter : DatasetImporter
{
    protected override List<List<string>> ReadRows(byte[] content)
    {
        using var stream = new MemoryStream(content);
        using var doc = SpreadsheetDocument.Open(stream, false);
        var wb = doc.WorkbookPart ?? throw new InvalidDataException("Çalışma kitabı bulunamadı");
        var sheet = wb.Workbook.Descendants<S.Sheet>().First();
        var wsPart = (WorksheetPart)wb.GetPartById(sheet.Id.Value);
        var shared = wb.SharedStringTablePart?.SharedStringTable?.Elements<S.SharedStringItem>().Select(s => s.InnerText).ToList()
                     ?? new List<string>();

        var rows = new List<List<string>>();
        foreach (var row in wsPart.Worksheet.Descendants<S.Row>())
        {
            var values = new List<string>();
            foreach (var cell in row.Elements<S.Cell>())
            {
                var col = ColumnIndex(cell.CellReference?.Value);
                while (col >= 0 && values.Count < col) values.Add("");
                values.Add(CellText(cell, shared));
            }
            rows.Add(values);
        }
        return rows;
    }

    private static string CellText(S.Cell cell, List<string> shared)
    {
        var raw = cell.CellValue?.Text;
        var type = cell.DataType?.Value;
        if (type == S.CellValues.SharedString && int.TryParse(raw, out var idx) && idx < shared.Count)
            return shared[idx];
        if (type == S.CellValues.InlineString)
            return cell.InlineString?.InnerText ?? "";
        if (raw == null) return "";
        // Sayısal hücreler: tam sayıları bilimsel gösterim olmadan yaz (ör. TC kimlik no)
        if ((type == null || type == S.CellValues.Number) &&
            double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var num) && num == Math.Floor(num) && Math.Abs(num) < 1e15)
            return ((long)num).ToString(CultureInfo.InvariantCulture);
        return raw;
    }

    private static int ColumnIndex(string reference)
    {
        if (string.IsNullOrEmpty(reference)) return -1;
        var index = 0;
        foreach (var ch in reference)
        {
            if (!char.IsLetter(ch)) break;
            index = index * 26 + (char.ToUpperInvariant(ch) - 'A' + 1);
        }
        return index - 1;
    }
}

public static class ImporterFactory
{
    public static DatasetImporter ForExtension(string ext) => ext.ToLowerInvariant() switch
    {
        ".csv" => new CsvDatasetImporter(),
        ".xlsx" => new XlsxDatasetImporter(),
        _ => throw new ArgumentException("Desteklenmeyen veri seti formatı")
    };
}

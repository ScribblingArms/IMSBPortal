using System.Xml.Linq;
using Microsoft.AspNetCore.Mvc;

namespace IMSB.Admin.Tests;

internal static class ExcelExportTestHelpers
{
    private static readonly XNamespace Spreadsheet = "urn:schemas-microsoft-com:office:spreadsheet";

    public static List<string?[]> Rows(byte[] content)
    {
        using var stream = new MemoryStream(content);
        var document = XDocument.Load(stream);
        return document.Descendants(Spreadsheet + "Row")
            .Select(row => row.Elements(Spreadsheet + "Cell")
                .Select(cell => (string?)cell.Element(Spreadsheet + "Data"))
                .ToArray())
            .ToList();
    }

    public static string CsvText(FileContentResult file)
    {
        Assert.Equal("application/vnd.ms-excel", file.ContentType);
        Assert.EndsWith(".xls", file.FileDownloadName);
        return CsvText(Rows(file.FileContents));
    }

    public static string CsvText(string xml)
    {
        var document = XDocument.Parse(xml);
        var rows = document.Descendants(Spreadsheet + "Row")
            .Select(row => row.Elements(Spreadsheet + "Cell")
                .Select(cell => (string?)cell.Element(Spreadsheet + "Data"))
                .ToArray())
            .ToList();
        return CsvText(rows);
    }

    private static string CsvText(IEnumerable<string?[]> rows) =>
        string.Join(Environment.NewLine, rows.Select(row => string.Join(",", row.Select(Escape)))) + Environment.NewLine;

    private static string Escape(string? value)
    {
        value ??= string.Empty;
        if (value.Contains('"') || value.Contains(',') || value.Contains('\n') || value.Contains('\r'))
            return '"' + value.Replace("\"", "\"\"") + '"';
        return value;
    }
}

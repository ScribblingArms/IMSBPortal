using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace IMSB.Common
{

    public static class CommonHelpers
    {
        /// <summary>First error per field, keyed by the snake_case names the forms use for their err_* elements.</summary>
        public static Dictionary<string, string> ModelStateErrors(ModelStateDictionary modelState)
        {
            var errors = new Dictionary<string, string>();
            foreach (var (key, entry) in modelState)
            {
                var message = entry.Errors.FirstOrDefault()?.ErrorMessage;
                if (!string.IsNullOrEmpty(message)) errors.TryAdd(ToErrorKey(key), message);
            }
            return errors;
        }

        public static bool TryParseDate(string? value, out DateTime date) =>
            DateTime.TryParseExact(value?.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date);

        private static readonly Regex Whitespace = new(@"\s+", RegexOptions.Compiled);
        private static readonly Regex TrailingNumber = new(@"(\d+)$", RegexOptions.Compiled);
        private static readonly Regex IndexSegment = new(@"\[(\d+)\]", RegexOptions.Compiled);

        public static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : Whitespace.Replace(s.Trim(), " ");

        public static string Date(DateTime? d) => d?.ToString("yyyy-MM-dd") ?? string.Empty;

        /// <summary>A UTC timestamp read from the database (Kind unspecified) marked as UTC, so JSON carries the "Z".</summary>
        public static DateTime Utc(DateTime d) => DateTime.SpecifyKind(d, DateTimeKind.Utc);

        /// <summary>yyyy-MM-dd of a stored UTC timestamp in server local time; empty when null.</summary>
        public static string LocalDate(DateTime? utc) => utc == null ? string.Empty : Date(Utc(utc.Value).ToLocalTime());

        public static string Money(decimal v) => v.ToString("0.00", CultureInfo.InvariantCulture);

        public static string Int(int v) => v.ToString(CultureInfo.InvariantCulture);

        /// <summary>400 with a single error, in the { errors: { key: message } } shape the pages read.</summary>
        public static BadRequestObjectResult ErrorResult(string key, string message) =>
            new(new { errors = new Dictionary<string, string> { [key] = message } });

        public static FileContentResult ExportExcel(string fileName, IEnumerable<IEnumerable<string?>> rows)
        {
            using var stream = new MemoryStream();
            using (var writer = XmlWriter.Create(stream, new XmlWriterSettings
            {
                Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true),
                Indent = true
            }))
            {
                const string spreadsheetNamespace = "urn:schemas-microsoft-com:office:spreadsheet";
                writer.WriteStartDocument();
                writer.WriteStartElement("Workbook", spreadsheetNamespace);
                writer.WriteAttributeString("xmlns", "ss", null, spreadsheetNamespace);
                writer.WriteStartElement("Worksheet", spreadsheetNamespace);
                writer.WriteAttributeString("ss", "Name", spreadsheetNamespace, "Report");
                writer.WriteStartElement("Table", spreadsheetNamespace);

                foreach (var row in rows)
                {
                    writer.WriteStartElement("Row", spreadsheetNamespace);
                    foreach (var value in row)
                    {
                        writer.WriteStartElement("Cell", spreadsheetNamespace);
                        writer.WriteStartElement("Data", spreadsheetNamespace);
                        writer.WriteAttributeString("ss", "Type", spreadsheetNamespace, "String");
                        writer.WriteString(value ?? string.Empty);
                        writer.WriteEndElement();
                        writer.WriteEndElement();
                    }
                    writer.WriteEndElement();
                }

                writer.WriteEndElement();
                writer.WriteEndElement();
                writer.WriteEndElement();
                writer.WriteEndDocument();
            }

            return new FileContentResult(stream.ToArray(), "application/vnd.ms-excel") { FileDownloadName = fileName };
        }

        /// <summary>Next PREFIX-0001 style number after the highest existing one.</summary>
        public static string NextCode(IEnumerable<string> existing, string prefix, int width) =>
            prefix + (existing
                .Select(c => TrailingNumber.Match(c))
                .Select(m => m.Success && int.TryParse(m.Value, out var n) ? n : 0)
                .DefaultIfEmpty(0).Max() + 1).ToString("D" + width);

        public static string CsvEscape(string? input)
        {
            if (string.IsNullOrEmpty(input)) return string.Empty;
            // a leading = + - @ makes Excel evaluate the cell as a formula (CSV injection); force it to text
            if ("=+-@\t\r".IndexOf(input[0]) >= 0) input = "'" + input;
            if (input.Contains('"') || input.Contains(',') || input.Contains('\n') || input.Contains('\r'))
            {
                return '"' + input.Replace("\"", "\"\"") + '"';
            }
            return input;
        }
        public static string ToErrorKey(string key)
        {
            if (key.StartsWith("$")) return "items";   // malformed JSON body
            var parts = IndexSegment.Replace(key, ".$1").Split('.', StringSplitOptions.RemoveEmptyEntries);
            // binding a [FromBody] model may prefix the parameter name ("dto.Date")
            if (parts.Length > 1 && parts[0].Equals("dto", StringComparison.OrdinalIgnoreCase)) parts = parts[1..];
            return string.Join('_', parts.Select(p =>
            {
                var sb = new StringBuilder();
                for (int i = 0; i < p.Length; i++)
                {
                    if (char.IsUpper(p[i]) && i > 0) sb.Append('_');
                    sb.Append(char.ToLowerInvariant(p[i]));
                }
                return sb.ToString();
            }));
        }
    }
    }

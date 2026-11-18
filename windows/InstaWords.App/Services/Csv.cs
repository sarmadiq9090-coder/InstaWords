using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace InstaWords.App.Services
{
    /// <summary>
    /// Minimal RFC 4180 CSV, ported from src/csv.ts so the app imports and exports exactly like the website:
    /// quotes, CRLF, BOM, and "," ";" or tab delimiters; a first row without any digit is a header.
    /// </summary>
    public static class Csv
    {
        /// <summary>Largest file the bulk tab imports (it is read into memory whole).</summary>
        public const long MaxImportBytes = 20L * 1024 * 1024;
        /// <summary>Most numbers the bulk tab loads at once, from a file, a paste or a drop.</summary>
        public const int MaxImportRows = 100000;
        /// <summary>How much of a file <see cref="Inspect"/> looks at.</summary>
        public const int HeadBytes = 4096;

        /// <summary>Why a file is not imported (checked before it is read).</summary>
        public enum ImportProblem
        {
            None,
            /// <summary>An Excel/Calc workbook (.xlsx, .xls…): it must be saved as CSV first.</summary>
            Spreadsheet,
            /// <summary>Larger than <see cref="MaxImportBytes"/>.</summary>
            TooLarge,
            /// <summary>Not text (a zip, PDF or other binary file), which would be read as garbage.</summary>
            NotText,
        }

        private static readonly HashSet<string> SpreadsheetExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".xlsx", ".xlsm", ".xlsb", ".xltx", ".xltm", ".xls", ".xlt", ".ods", ".numbers",
        };

        /// <summary>From the file name, its size and its first bytes (<see cref="ReadHead"/>).</summary>
        public static ImportProblem Inspect(string fileName, long size, byte[] head)
        {
            if (SpreadsheetExtensions.Contains(Path.GetExtension(fileName ?? "") ?? "")) return ImportProblem.Spreadsheet;
            if (size > MaxImportBytes) return ImportProblem.TooLarge;
            return LooksBinary(head) ? ImportProblem.NotText : ImportProblem.None;
        }

        /// <summary>
        /// Text files have no NUL bytes, except UTF-16 (which starts with its BOM). Zip (xlsx, docx), OLE (xls, doc)
        /// and PDF files are recognised by their signature even when renamed to .csv.
        /// </summary>
        public static bool LooksBinary(byte[] head)
        {
            if (head == null || head.Length == 0) return false;
            if (head.Length >= 2 && ((head[0] == 0xFF && head[1] == 0xFE) || (head[0] == 0xFE && head[1] == 0xFF))) return false;
            if (StartsWith(head, 0x50, 0x4B, 0x03, 0x04) || StartsWith(head, 0xD0, 0xCF, 0x11, 0xE0) || StartsWith(head, 0x25, 0x50, 0x44, 0x46)) return true;
            return Array.IndexOf(head, (byte)0) >= 0;
        }

        private static bool StartsWith(byte[] bytes, params byte[] prefix)
        {
            if (bytes.Length < prefix.Length) return false;
            for (var i = 0; i < prefix.Length; i++)
                if (bytes[i] != prefix[i]) return false;
            return true;
        }

        /// <summary>"20" or "35.2": a size in megabytes for messages (MB = 1,048,576 bytes).</summary>
        public static string Megabytes(long bytes) =>
            (bytes / 1048576.0).ToString("0.#", CultureInfo.InvariantCulture);

        /// <summary>The first <paramref name="count"/> bytes of a file (fewer if it is shorter).</summary>
        public static byte[] ReadHead(string path, int count)
        {
            using (var fs = OpenShared(path))
            {
                var buffer = new byte[(int)Math.Min(count, fs.Length)];
                var read = 0;
                while (read < buffer.Length)
                {
                    var n = fs.Read(buffer, read, buffer.Length - read);
                    if (n <= 0) break;
                    read += n;
                }
                if (read < buffer.Length) Array.Resize(ref buffer, read);
                return buffer;
            }
        }

        /// <summary>Reading only, and still allowed while Excel has the file open.</summary>
        private static FileStream OpenShared(string path) =>
            new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);

        public static List<List<string>> Parse(string text, char delimiter)
        {
            text = text ?? "";
            if (text.Length > 0 && text[0] == '﻿') text = text.Substring(1);
            var rows = new List<List<string>>();
            var row = new List<string>();
            var cell = new StringBuilder();
            var quoted = false;
            for (var i = 0; i < text.Length; i++)
            {
                var ch = text[i];
                if (quoted)
                {
                    if (ch == '"')
                    {
                        if (i + 1 < text.Length && text[i + 1] == '"') { cell.Append('"'); i++; }
                        else quoted = false;
                    }
                    else cell.Append(ch);
                }
                else if (ch == '"' && cell.Length == 0) quoted = true;
                else if (ch == delimiter) { row.Add(cell.ToString()); cell.Clear(); }
                else if (ch == '\n' || ch == '\r')
                {
                    if (ch == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
                    row.Add(cell.ToString()); rows.Add(row); row = new List<string>(); cell.Clear();
                }
                else cell.Append(ch);
            }
            if (cell.Length > 0 || row.Count > 0) { row.Add(cell.ToString()); rows.Add(row); }
            return rows;
        }

        public static char DetectDelimiter(string text)
        {
            text = text ?? "";
            var nl = text.IndexOfAny(new[] { '\r', '\n' });
            var first = nl < 0 ? text : text.Substring(0, nl);
            // A comma inside a single number ("1,500") is common, so prefer ; and tab when present.
            if (first.IndexOf('\t') >= 0) return '\t';
            if (first.IndexOf(';') >= 0) return ';';
            return ',';
        }

        /// <summary>The values of a one-column CSV/TXT. With "," as delimiter, an unquoted "1,500" stays one value.</summary>
        public static List<string> ColumnValues(string text)
        {
            var delim = DetectDelimiter(text);
            var values = Parse(text, delim == ',' ? '\0' : delim)
                .Select(r => (r.Count > 0 ? r[0] : "").Trim())
                .Where(v => v.Length > 0)
                .ToList();
            if (values.Count > 0 && !values[0].Any(IsDigit)) values.RemoveAt(0);
            return values;
        }

        /// <summary>
        /// Lines pasted from Excel or typed: one value per line. Excel copies a column as one cell per line;
        /// for a multi-column selection the first cell of each line is used.
        /// </summary>
        public static List<string> PastedValues(string text)
        {
            var values = new List<string>();
            foreach (var line in (text ?? "").Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None))
            {
                var v = line;
                var tab = v.IndexOf('\t');
                if (tab >= 0) v = v.Substring(0, tab);
                v = v.Trim();
                if (v.Length > 1 && v[0] == '"' && v[v.Length - 1] == '"') v = v.Substring(1, v.Length - 2).Replace("\"\"", "\"").Trim();
                if (v.Length > 0) values.Add(v);
            }
            return values;
        }

        public static bool IsDigit(char c) => (c >= '0' && c <= '9') || (c >= '٠' && c <= '٩') || (c >= '۰' && c <= '۹');

        /// <summary>A leading = + - @ tab or CR would make a spreadsheet run the cell as a formula: prefix it with '.</summary>
        public static string SafeCell(string v)
        {
            if (string.IsNullOrEmpty(v)) return v ?? "";
            var c = v[0];
            if (c == '=' || c == '+' || c == '@' || c == '\t' || c == '\r') return "'" + v;
            // "-5" is a plain number, "-1+2" or "-cmd" is not.
            if (c == '-' && !IsPlainNumber(v)) return "'" + v;
            return v;
        }

        private static bool IsPlainNumber(string v)
        {
            var hasDigit = false;
            for (var i = 0; i < v.Length; i++)
            {
                var ch = v[i];
                if (IsDigit(ch)) hasDigit = true;
                else if (i == 0 && ch == '-') { }
                else if (ch == '.' || ch == ',' || ch == '٫' || ch == '٬' || ch == ' ') { }
                else return false;
            }
            return hasDigit;
        }

        private static string Escape(string v) =>
            v.IndexOfAny(new[] { '"', ',', '\r', '\n', ';' }) >= 0 ? "\"" + v.Replace("\"", "\"\"") + "\"" : v;

        /// <summary>CSV text with CRLF line ends (write it with a UTF-8 BOM so Excel reads Arabic correctly).</summary>
        public static string Write(IEnumerable<IEnumerable<string>> rows)
        {
            var sb = new StringBuilder();
            foreach (var r in rows) sb.Append(string.Join(",", r.Select(c => Escape(c ?? "")))).Append("\r\n");
            return sb.ToString();
        }

        public static void Save(string path, IEnumerable<IEnumerable<string>> rows) =>
            File.WriteAllText(path, Write(rows), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

        /// <summary>
        /// Reads a text file: UTF-8/UTF-16 by BOM, else strict UTF-8, else the Arabic Windows code page (1256),
        /// which older Excel versions use for "CSV (comma delimited)".
        /// </summary>
        public static string ReadText(string path)
        {
            byte[] bytes;
            using (var fs = OpenShared(path))
            {
                if (fs.Length > MaxImportBytes) throw new IOException("larger than " + Megabytes(MaxImportBytes) + " MB");
                bytes = new byte[fs.Length];
                var read = 0;
                while (read < bytes.Length)
                {
                    var n = fs.Read(bytes, read, bytes.Length - read);
                    if (n <= 0) break;
                    read += n;
                }
                if (read < bytes.Length) Array.Resize(ref bytes, read);
            }
            if (bytes.Length >= 2 && ((bytes[0] == 0xFF && bytes[1] == 0xFE) || (bytes[0] == 0xFE && bytes[1] == 0xFF)) ||
                bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            {
                using (var r = new StreamReader(new MemoryStream(bytes), Encoding.UTF8, detectEncodingFromByteOrderMarks: true))
                    return r.ReadToEnd();
            }
            try
            {
                return new UTF8Encoding(false, throwOnInvalidBytes: true).GetString(bytes);
            }
            catch (DecoderFallbackException)
            {
                return Encoding.GetEncoding(1256).GetString(bytes);
            }
        }
    }
}

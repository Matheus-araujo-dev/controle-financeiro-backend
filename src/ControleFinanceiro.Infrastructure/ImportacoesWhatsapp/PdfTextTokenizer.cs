using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;

namespace ControleFinanceiro.Infrastructure.ImportacoesWhatsapp;

internal static partial class PdfTextTokenizer
{
    public static IReadOnlyCollection<string> ExtractTokens(byte[] pdfBytes)
    {
        if (pdfBytes.Length == 0)
        {
            return Array.Empty<string>();
        }

        var pdfText = Encoding.Latin1.GetString(pdfBytes);
        var streams = ExtractStreams(pdfBytes, pdfText);
        if (streams.Count == 0)
        {
            return Array.Empty<string>();
        }

        var cMap = BuildCMap(streams);
        var tokens = new List<string>();

        foreach (var stream in streams)
        {
            tokens.AddRange(ExtractTokensFromStream(stream, cMap));
        }

        return tokens
            .Select(NormalizeToken)
            .Where(token => !string.IsNullOrWhiteSpace(token))
            .ToArray();
    }

    private static List<byte[]> ExtractStreams(byte[] pdfBytes, string pdfText)
    {
        var streams = new List<byte[]>();
        var position = 0;

        while (true)
        {
            var streamStart = pdfText.IndexOf("stream", position, StringComparison.Ordinal);
            if (streamStart < 0)
            {
                break;
            }

            var dataStart = streamStart + "stream".Length;
            if (dataStart < pdfBytes.Length && pdfBytes[dataStart] == '\r')
            {
                dataStart++;
            }

            if (dataStart < pdfBytes.Length && pdfBytes[dataStart] == '\n')
            {
                dataStart++;
            }

            var streamEnd = pdfText.IndexOf("endstream", dataStart, StringComparison.Ordinal);
            if (streamEnd < 0)
            {
                break;
            }

            var rawData = new byte[streamEnd - dataStart];
            Array.Copy(pdfBytes, dataStart, rawData, 0, rawData.Length);
            streams.Add(TryDecompress(rawData) ?? rawData);

            position = streamEnd + "endstream".Length;
        }

        return streams;
    }

    private static byte[]? TryDecompress(byte[] rawData)
    {
        try
        {
            using var input = new MemoryStream(rawData);
            using var zlib = new ZLibStream(input, CompressionMode.Decompress);
            using var output = new MemoryStream();
            zlib.CopyTo(output);
            return output.ToArray();
        }
        catch (InvalidDataException)
        {
            return null;
        }
        catch (NotSupportedException)
        {
            return null;
        }
    }

    private static Dictionary<int, string> BuildCMap(IReadOnlyCollection<byte[]> streams)
    {
        var map = new Dictionary<int, string>();

        foreach (var stream in streams)
        {
            var text = Encoding.Latin1.GetString(stream);
            if (!text.Contains("begincmap", StringComparison.Ordinal))
            {
                continue;
            }

            var inCharSection = false;
            var inRangeSection = false;

            foreach (var rawLine in text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
            {
                var line = rawLine.Trim();
                if (line.Contains("beginbfchar", StringComparison.Ordinal))
                {
                    inCharSection = true;
                    continue;
                }

                if (line.Contains("endbfchar", StringComparison.Ordinal))
                {
                    inCharSection = false;
                    continue;
                }

                if (line.Contains("beginbfrange", StringComparison.Ordinal))
                {
                    inRangeSection = true;
                    continue;
                }

                if (line.Contains("endbfrange", StringComparison.Ordinal))
                {
                    inRangeSection = false;
                    continue;
                }

                if (inCharSection)
                {
                    var charMatch = CMapCharRegex().Match(line);
                    if (charMatch.Success)
                    {
                        map[ParseHexInt(charMatch.Groups["source"].Value)] = ParseUnicodeHex(charMatch.Groups["target"].Value);
                    }
                }

                if (inRangeSection)
                {
                    var rangeMatch = CMapRangeRegex().Match(line);
                    if (!rangeMatch.Success)
                    {
                        continue;
                    }

                    var sourceStart = ParseHexInt(rangeMatch.Groups["sourceStart"].Value);
                    var sourceEnd = ParseHexInt(rangeMatch.Groups["sourceEnd"].Value);
                    var targetStart = ParseHexInt(rangeMatch.Groups["targetStart"].Value);

                    for (var offset = 0; offset <= sourceEnd - sourceStart; offset++)
                    {
                        map[sourceStart + offset] = char.ConvertFromUtf32(targetStart + offset);
                    }
                }
            }
        }

        return map;
    }

    private static IEnumerable<string> ExtractTokensFromStream(byte[] stream, IReadOnlyDictionary<int, string> cMap)
    {
        var tokens = new List<string>();

        for (var index = 0; index < stream.Length; index++)
        {
            if (stream[index] != '(')
            {
                continue;
            }

            if (!TryReadLiteralString(stream, index, out var endIndex, out var literalBytes))
            {
                continue;
            }

            var operatorIndex = SkipWhitespace(stream, endIndex + 1);
            if (!IsTextShowOperator(stream, operatorIndex))
            {
                index = endIndex;
                continue;
            }

            var decoded = DecodeLiteralString(literalBytes, cMap);
            if (!string.IsNullOrWhiteSpace(decoded))
            {
                tokens.Add(decoded);
            }

            index = endIndex;
        }

        return tokens;
    }

    private static bool TryReadLiteralString(byte[] stream, int startIndex, out int endIndex, out byte[] bytes)
    {
        var buffer = new List<byte>();
        var depth = 0;
        var escaping = false;

        for (var index = startIndex; index < stream.Length; index++)
        {
            var current = stream[index];

            if (index == startIndex)
            {
                depth = 1;
                continue;
            }

            if (escaping)
            {
                if (current is >= (byte)'0' and <= (byte)'7')
                {
                    var octalDigits = new List<byte> { current };
                    for (var octalIndex = index + 1; octalIndex < stream.Length && octalDigits.Count < 3; octalIndex++)
                    {
                        if (stream[octalIndex] is < (byte)'0' or > (byte)'7')
                        {
                            break;
                        }

                        octalDigits.Add(stream[octalIndex]);
                        index = octalIndex;
                    }

                    buffer.Add(Convert.ToByte(Encoding.ASCII.GetString(octalDigits.ToArray()), 8));
                }
                else
                {
                    buffer.Add(current switch
                    {
                        (byte)'n' => (byte)'\n',
                        (byte)'r' => (byte)'\r',
                        (byte)'t' => (byte)'\t',
                        (byte)'b' => (byte)'\b',
                        (byte)'f' => (byte)'\f',
                        _ => current
                    });
                }

                escaping = false;
                continue;
            }

            if (current == '\\')
            {
                escaping = true;
                continue;
            }

            if (current == '(')
            {
                depth++;
                buffer.Add(current);
                continue;
            }

            if (current == ')')
            {
                depth--;
                if (depth == 0)
                {
                    endIndex = index;
                    bytes = buffer.ToArray();
                    return true;
                }

                buffer.Add(current);
                continue;
            }

            buffer.Add(current);
        }

        endIndex = startIndex;
        bytes = Array.Empty<byte>();
        return false;
    }

    private static int SkipWhitespace(byte[] stream, int startIndex)
    {
        var index = startIndex;
        while (index < stream.Length && char.IsWhiteSpace((char)stream[index]))
        {
            index++;
        }

        return index;
    }

    private static bool IsTextShowOperator(byte[] stream, int operatorIndex)
    {
        return operatorIndex + 1 < stream.Length &&
               stream[operatorIndex] == 'T' &&
               stream[operatorIndex + 1] == 'j';
    }

    private static string DecodeLiteralString(byte[] literalBytes, IReadOnlyDictionary<int, string> cMap)
    {
        if (literalBytes.Length == 0)
        {
            return string.Empty;
        }

        if (cMap.Count > 0 && literalBytes.Length % 2 == 0)
        {
            var builder = new StringBuilder();
            var mappedCount = 0;

            for (var index = 0; index < literalBytes.Length; index += 2)
            {
                var code = (literalBytes[index] << 8) | literalBytes[index + 1];
                if (cMap.TryGetValue(code, out var mapped))
                {
                    builder.Append(mapped);
                    mappedCount++;
                }
                else
                {
                    builder.Append((char)code);
                }
            }

            if (mappedCount > 0)
            {
                return builder.ToString();
            }
        }

        if (literalBytes.Length % 2 == 0 && literalBytes.Any(value => value == 0))
        {
            return Encoding.BigEndianUnicode.GetString(literalBytes);
        }

        return Encoding.Latin1.GetString(literalBytes);
    }

    private static int ParseHexInt(string value)
    {
        return int.Parse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
    }

    private static string ParseUnicodeHex(string value)
    {
        var builder = new StringBuilder();
        for (var index = 0; index < value.Length; index += 4)
        {
            builder.Append(char.ConvertFromUtf32(ParseHexInt(value.Substring(index, 4))));
        }

        return builder.ToString();
    }

    private static string NormalizeToken(string value)
    {
        return Regex.Replace(value, "\\s+", " ").Trim();
    }

    [GeneratedRegex(@"<(?<source>[0-9A-Fa-f]{4})><(?<target>[0-9A-Fa-f]{4,})>")]
    private static partial Regex CMapCharRegex();

    [GeneratedRegex(@"<(?<sourceStart>[0-9A-Fa-f]{4})><(?<sourceEnd>[0-9A-Fa-f]{4})><(?<targetStart>[0-9A-Fa-f]{4})>")]
    private static partial Regex CMapRangeRegex();
}

using System.Numerics;
using System.Text.Json;
using Rdn;
using Xunit;
using Xunit.Abstractions;

namespace Rdn.Tests;

/// <summary>
/// Conformance tests that validate the C# RDN implementation against the shared
/// language-agnostic test suite in test-suite/.
///
/// Valid tests: parse .rdn and compare against .expected.json using the $type tagged convention.
/// Invalid tests: parse .rdn and assert RdnException is thrown.
/// Roundtrip tests: parse .rdn, serialize back, parse again, assert deep equality.
/// </summary>
public class ConformanceTests
{
    private readonly ITestOutputHelper _output;

    public ConformanceTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private static string GetTestSuiteDir()
    {
        // Walk up from the test assembly output directory to find the test-suite folder.
        // The .csproj copies test-suite files to the output directory under "test-suite/".
        string baseDir = AppContext.BaseDirectory;
        string testSuiteDir = Path.Combine(baseDir, "test-suite");
        if (Directory.Exists(testSuiteDir))
            return testSuiteDir;

        // Fallback: try the relative path from the project file location
        string? dir = baseDir;
        for (int i = 0; i < 10 && dir != null; i++)
        {
            string candidate = Path.Combine(dir, "test-suite");
            if (Directory.Exists(candidate))
                return candidate;
            dir = Directory.GetParent(dir)?.FullName;
        }

        throw new DirectoryNotFoundException("Could not locate test-suite directory");
    }

    #region Valid Tests

    public static IEnumerable<object[]> ValidTestData()
    {
        string testSuiteDir;
        try
        {
            testSuiteDir = GetTestSuiteDir();
        }
        catch (DirectoryNotFoundException)
        {
            yield break;
        }

        string validDir = Path.Combine(testSuiteDir, "valid");
        if (!Directory.Exists(validDir))
            yield break;

        foreach (string rdnFile in Directory.GetFiles(validDir, "*.rdn").OrderBy(f => f))
        {
            string baseName = Path.GetFileNameWithoutExtension(rdnFile);
            string expectedFile = Path.Combine(validDir, baseName + ".expected.json");
            if (File.Exists(expectedFile))
            {
                yield return new object[] { baseName, rdnFile, expectedFile };
            }
        }
    }

    [Theory]
    [MemberData(nameof(ValidTestData))]
    public void Valid_ParseAndCompareExpected(string testName, string rdnFilePath, string expectedJsonPath)
    {
        string rdnText = File.ReadAllText(rdnFilePath);
        string expectedJsonText = File.ReadAllText(expectedJsonPath);

        _output.WriteLine($"Test: {testName}");
        _output.WriteLine($"RDN: {rdnText.TrimEnd()}");

        using var rdnDoc = RdnDocument.Parse(rdnText);
        using var expectedDoc = JsonDocument.Parse(expectedJsonText);

        AssertRdnMatchesExpectedJson(rdnDoc.RootElement, expectedDoc.RootElement, "$");
    }

    #endregion

    #region Invalid Tests

    public static IEnumerable<object[]> InvalidTestData()
    {
        string testSuiteDir;
        try
        {
            testSuiteDir = GetTestSuiteDir();
        }
        catch (DirectoryNotFoundException)
        {
            yield break;
        }

        string invalidDir = Path.Combine(testSuiteDir, "invalid");
        if (!Directory.Exists(invalidDir))
            yield break;

        foreach (string rdnFile in Directory.GetFiles(invalidDir, "*.rdn").OrderBy(f => f))
        {
            string baseName = Path.GetFileNameWithoutExtension(rdnFile);
            yield return new object[] { baseName, rdnFile };
        }
    }

    [Theory]
    [MemberData(nameof(InvalidTestData))]
    public void Invalid_ParseShouldThrow(string testName, string rdnFilePath)
    {
        string rdnText = File.ReadAllText(rdnFilePath);

        _output.WriteLine($"Test: {testName}");
        _output.WriteLine($"RDN: {rdnText.TrimEnd()}");

        Assert.ThrowsAny<RdnException>(() =>
        {
            using var doc = RdnDocument.Parse(rdnText);
            // Force evaluation of the root element to trigger lazy parsing errors
            _ = doc.RootElement.ValueKind;
        });
    }

    #endregion

    #region Roundtrip Tests

    public static IEnumerable<object[]> RoundtripTestData()
    {
        string testSuiteDir;
        try
        {
            testSuiteDir = GetTestSuiteDir();
        }
        catch (DirectoryNotFoundException)
        {
            yield break;
        }

        string roundtripDir = Path.Combine(testSuiteDir, "roundtrip");
        if (!Directory.Exists(roundtripDir))
            yield break;

        foreach (string rdnFile in Directory.GetFiles(roundtripDir, "*.rdn").OrderBy(f => f))
        {
            string baseName = Path.GetFileNameWithoutExtension(rdnFile);
            yield return new object[] { baseName, rdnFile };
        }
    }

    [Theory]
    [MemberData(nameof(RoundtripTestData))]
    public void Roundtrip_ParseSerializeParseEquals(string testName, string rdnFilePath)
    {
        string rdnText = File.ReadAllText(rdnFilePath);

        _output.WriteLine($"Test: {testName}");
        _output.WriteLine($"Original RDN: {rdnText.TrimEnd()}");

        // Parse first time
        using var doc1 = RdnDocument.Parse(rdnText);

        // Serialize back to string
        string serialized;
        using (var stream = new MemoryStream())
        {
            using (var writer = new Utf8RdnWriter(stream))
            {
                doc1.WriteTo(writer);
            }
            serialized = System.Text.Encoding.UTF8.GetString(stream.ToArray());
        }

        _output.WriteLine($"Serialized: {serialized}");

        // Parse second time
        using var doc2 = RdnDocument.Parse(serialized);

        // Assert deep equality
        Assert.True(
            RdnElement.DeepEquals(doc1.RootElement, doc2.RootElement),
            $"Roundtrip failed for {testName}. Original: {rdnText.TrimEnd()}, Serialized: {serialized}");
    }

    #endregion

    #region Comparison Logic

    /// <summary>
    /// Recursively compares a parsed RDN element against an expected JSON element
    /// using the $type tagged convention for extended types.
    /// </summary>
    private void AssertRdnMatchesExpectedJson(RdnElement rdnElement, JsonElement expectedJson, string path)
    {
        // Check if the expected JSON uses the $type tagged convention
        if (expectedJson.ValueKind == JsonValueKind.Object && TryGetTaggedType(expectedJson, out string? typeName))
        {
            AssertTaggedType(rdnElement, expectedJson, typeName!, path);
            return;
        }

        switch (expectedJson.ValueKind)
        {
            case JsonValueKind.Null:
                Assert.True(rdnElement.ValueKind == RdnValueKind.Null, $"{path}: Expected Null, got {rdnElement.ValueKind}");
                break;

            case JsonValueKind.True:
                Assert.True(rdnElement.ValueKind == RdnValueKind.True, $"{path}: Expected True, got {rdnElement.ValueKind}");
                break;

            case JsonValueKind.False:
                Assert.True(rdnElement.ValueKind == RdnValueKind.False, $"{path}: Expected False, got {rdnElement.ValueKind}");
                break;

            case JsonValueKind.Number:
                Assert.True(rdnElement.ValueKind == RdnValueKind.Number, $"{path}: Expected Number, got {rdnElement.ValueKind}");
                double expectedNum = expectedJson.GetDouble();
                double actualNum = rdnElement.GetDouble();
                Assert.Equal(expectedNum, actualNum);
                break;

            case JsonValueKind.String:
                Assert.True(rdnElement.ValueKind == RdnValueKind.String, $"{path}: Expected String, got {rdnElement.ValueKind}");
                Assert.Equal(expectedJson.GetString(), rdnElement.GetString());
                break;

            case JsonValueKind.Array:
                // Could be a plain array or a tuple (tuples parse as arrays)
                Assert.True(rdnElement.ValueKind == RdnValueKind.Array, $"{path}: Expected Array, got {rdnElement.ValueKind}");
                int expectedLen = expectedJson.GetArrayLength();
                int actualLen = rdnElement.GetArrayLength();
                Assert.Equal(expectedLen, actualLen);
                int i = 0;
                var expectedEnumerator = expectedJson.EnumerateArray();
                foreach (var rdnItem in rdnElement.EnumerateArray())
                {
                    expectedEnumerator.MoveNext();
                    AssertRdnMatchesExpectedJson(rdnItem, expectedEnumerator.Current, $"{path}[{i}]");
                    i++;
                }
                break;

            case JsonValueKind.Object:
                Assert.True(rdnElement.ValueKind == RdnValueKind.Object, $"{path}: Expected Object, got {rdnElement.ValueKind}");
                foreach (var prop in expectedJson.EnumerateObject())
                {
                    var rdnProp = rdnElement.GetProperty(prop.Name);
                    AssertRdnMatchesExpectedJson(rdnProp, prop.Value, $"{path}.{prop.Name}");
                }
                break;

            default:
                Assert.Fail($"{path}: Unexpected expected JSON value kind: {expectedJson.ValueKind}");
                break;
        }
    }

    /// <summary>
    /// Checks if a JSON object uses the $type tagged convention.
    /// </summary>
    private static bool TryGetTaggedType(JsonElement jsonObj, out string? typeName)
    {
        typeName = null;
        if (jsonObj.ValueKind != JsonValueKind.Object)
            return false;

        if (jsonObj.TryGetProperty("$type", out JsonElement typeElement))
        {
            typeName = typeElement.GetString();
            return typeName != null;
        }

        return false;
    }

    /// <summary>
    /// Asserts that an RDN element matches a $type-tagged expected JSON value.
    /// </summary>
    private void AssertTaggedType(RdnElement rdnElement, JsonElement expectedJson, string typeName, string path)
    {
        switch (typeName)
        {
            case "Date":
            {
                Assert.True(rdnElement.ValueKind == RdnValueKind.RdnDateTime, $"{path}: Expected RdnDateTime, got {rdnElement.ValueKind}");
                string expectedIso = expectedJson.GetProperty("value").GetString()!;
                var expectedDt = DateTimeOffset.Parse(expectedIso, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.RoundtripKind);
                var actualDt = rdnElement.GetDateTimeOffset();
                Assert.Equal(expectedDt, actualDt);
                break;
            }

            case "BigInt":
            {
                Assert.True(rdnElement.ValueKind == RdnValueKind.RdnBigInteger, $"{path}: Expected RdnBigInteger, got {rdnElement.ValueKind}");
                string expectedValue = expectedJson.GetProperty("value").GetString()!;
                var expected = BigInteger.Parse(expectedValue);
                var actual = rdnElement.GetBigInteger();
                Assert.Equal(expected, actual);
                break;
            }

            case "Binary":
            {
                Assert.True(rdnElement.ValueKind == RdnValueKind.RdnBinary, $"{path}: Expected RdnBinary, got {rdnElement.ValueKind}");
                string expectedBase64 = expectedJson.GetProperty("value").GetString()!;
                byte[] expectedBytes = expectedBase64.Length > 0 ? Convert.FromBase64String(expectedBase64) : Array.Empty<byte>();
                byte[] actualBytes = rdnElement.GetRdnBinary();
                Assert.Equal(expectedBytes, actualBytes);
                break;
            }

            case "RegExp":
            {
                Assert.True(rdnElement.ValueKind == RdnValueKind.RdnRegExp, $"{path}: Expected RdnRegExp, got {rdnElement.ValueKind}");
                var valueObj = expectedJson.GetProperty("value");
                string expectedSource = valueObj.GetProperty("source").GetString()!;
                string expectedFlags = valueObj.GetProperty("flags").GetString()!;
                string actualSource = rdnElement.GetRdnRegExpSource();
                string actualFlags = rdnElement.GetRdnRegExpFlags();
                Assert.Equal(expectedSource, actualSource);
                Assert.Equal(expectedFlags, actualFlags);
                break;
            }

            case "TimeOnly":
            {
                Assert.True(rdnElement.ValueKind == RdnValueKind.RdnTimeOnly, $"{path}: Expected RdnTimeOnly, got {rdnElement.ValueKind}");
                var valueObj = expectedJson.GetProperty("value");
                int expectedHours = valueObj.GetProperty("hours").GetInt32();
                int expectedMinutes = valueObj.GetProperty("minutes").GetInt32();
                int expectedSeconds = valueObj.GetProperty("seconds").GetInt32();
                int expectedMs = valueObj.GetProperty("milliseconds").GetInt32();
                var actual = rdnElement.GetRdnTimeOnly();
                Assert.Equal(expectedHours, actual.Hour);
                Assert.Equal(expectedMinutes, actual.Minute);
                Assert.Equal(expectedSeconds, actual.Second);
                Assert.Equal(expectedMs, actual.Millisecond);
                break;
            }

            case "Duration":
            {
                Assert.True(rdnElement.ValueKind == RdnValueKind.RdnDuration, $"{path}: Expected RdnDuration, got {rdnElement.ValueKind}");
                string expectedIso = expectedJson.GetProperty("value").GetString()!;
                var actual = rdnElement.GetRdnDuration();
                Assert.Equal(expectedIso, actual.Iso);
                break;
            }

            case "Number":
            {
                // Special number values: NaN, Infinity, -Infinity
                Assert.True(rdnElement.ValueKind == RdnValueKind.Number, $"{path}: Expected Number (special), got {rdnElement.ValueKind}");
                string expectedValue = expectedJson.GetProperty("value").GetString()!;
                double actual = rdnElement.GetDouble();
                switch (expectedValue)
                {
                    case "NaN":
                        Assert.True(double.IsNaN(actual), $"{path}: Expected NaN, got {actual}");
                        break;
                    case "Infinity":
                        Assert.True(double.IsPositiveInfinity(actual), $"{path}: Expected Infinity, got {actual}");
                        break;
                    case "-Infinity":
                        Assert.True(double.IsNegativeInfinity(actual), $"{path}: Expected -Infinity, got {actual}");
                        break;
                    default:
                        Assert.Fail($"{path}: Unknown special number value: {expectedValue}");
                        break;
                }
                break;
            }

            case "Set":
            {
                Assert.True(rdnElement.ValueKind == RdnValueKind.Set, $"{path}: Expected Set, got {rdnElement.ValueKind}");
                var expectedArray = expectedJson.GetProperty("value");
                int expectedLen = expectedArray.GetArrayLength();
                int actualLen = rdnElement.GetArrayLength();
                Assert.Equal(expectedLen, actualLen);
                int idx = 0;
                var expectedEnumerator = expectedArray.EnumerateArray();
                foreach (var setItem in rdnElement.EnumerateSet())
                {
                    expectedEnumerator.MoveNext();
                    AssertRdnMatchesExpectedJson(setItem, expectedEnumerator.Current, $"{path}[{idx}]");
                    idx++;
                }
                break;
            }

            case "Map":
            {
                Assert.True(rdnElement.ValueKind == RdnValueKind.Map, $"{path}: Expected Map, got {rdnElement.ValueKind}");
                var expectedEntries = expectedJson.GetProperty("value");
                int expectedEntryCount = expectedEntries.GetArrayLength();

                // Map entries are stored flat (key, value, key, value...) in the RDN element
                var mapItems = rdnElement.EnumerateMap().ToList();
                Assert.Equal(expectedEntryCount * 2, mapItems.Count);

                int entryIdx = 0;
                foreach (var entry in expectedEntries.EnumerateArray())
                {
                    // Each entry is [key, value]
                    Assert.Equal(2, entry.GetArrayLength());
                    var expectedKey = entry[0];
                    var expectedValue = entry[1];
                    AssertRdnMatchesExpectedJson(mapItems[entryIdx * 2], expectedKey, $"{path}.entries[{entryIdx}].key");
                    AssertRdnMatchesExpectedJson(mapItems[entryIdx * 2 + 1], expectedValue, $"{path}.entries[{entryIdx}].value");
                    entryIdx++;
                }
                break;
            }

            case "Tuple":
            {
                // Tuples parse as arrays in the C# implementation
                Assert.True(rdnElement.ValueKind == RdnValueKind.Array, $"{path}: Expected Array (tuple), got {rdnElement.ValueKind}");
                var expectedArray = expectedJson.GetProperty("value");
                int expectedLen = expectedArray.GetArrayLength();
                int actualLen = rdnElement.GetArrayLength();
                Assert.Equal(expectedLen, actualLen);
                int idx = 0;
                var expectedEnumerator = expectedArray.EnumerateArray();
                foreach (var item in rdnElement.EnumerateArray())
                {
                    expectedEnumerator.MoveNext();
                    AssertRdnMatchesExpectedJson(item, expectedEnumerator.Current, $"{path}[{idx}]");
                    idx++;
                }
                break;
            }

            default:
                Assert.Fail($"{path}: Unknown $type: {typeName}");
                break;
        }
    }

    #endregion
}

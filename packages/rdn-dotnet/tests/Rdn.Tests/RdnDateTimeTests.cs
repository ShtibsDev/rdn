using Rdn;
using Rdn.Serialization;
using Xunit;

namespace Rdn.Tests;

public class RdnDateTimeTests
{
    #region 1. Utf8RdnReader — RdnDateTime parsing

    [Fact]
    public void Reader_DateTimeWithMilliseconds()
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes("@2024-01-15T10:30:00.123Z");
        var reader = new Utf8RdnReader(bytes);
        Assert.True(reader.Read());
        Assert.Equal(RdnTokenType.RdnDateTime, reader.TokenType);

        var dt = reader.GetRdnDateTime();
        Assert.Equal(new DateTime(2024, 1, 15, 10, 30, 0, 123, DateTimeKind.Utc), dt);
    }

    [Fact]
    public void Reader_DateTimeNoMilliseconds()
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes("@2024-01-15T10:30:00Z");
        var reader = new Utf8RdnReader(bytes);
        Assert.True(reader.Read());
        Assert.Equal(RdnTokenType.RdnDateTime, reader.TokenType);

        var dt = reader.GetRdnDateTime();
        Assert.Equal(new DateTime(2024, 1, 15, 10, 30, 0, DateTimeKind.Utc), dt);
    }

    [Fact]
    public void Reader_DateOnly()
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes("@2024-01-15");
        var reader = new Utf8RdnReader(bytes);
        Assert.True(reader.Read());
        Assert.Equal(RdnTokenType.RdnDateTime, reader.TokenType);

        var dt = reader.GetRdnDateTime();
        Assert.Equal(2024, dt.Year);
        Assert.Equal(1, dt.Month);
        Assert.Equal(15, dt.Day);
    }

    [Fact]
    public void Reader_UnixTimestampSeconds()
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes("@1705312200");
        var reader = new Utf8RdnReader(bytes);
        Assert.True(reader.Read());
        Assert.Equal(RdnTokenType.RdnDateTime, reader.TokenType);

        var dt = reader.GetRdnDateTime();
        var expected = DateTime.UnixEpoch.AddSeconds(1705312200);
        Assert.Equal(expected, dt);
    }

    [Fact]
    public void Reader_UnixTimestampMilliseconds()
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes("@1705312200123");
        var reader = new Utf8RdnReader(bytes);
        Assert.True(reader.Read());
        Assert.Equal(RdnTokenType.RdnDateTime, reader.TokenType);

        var dt = reader.GetRdnDateTime();
        var expected = DateTime.UnixEpoch.AddMilliseconds(1705312200123);
        Assert.Equal(expected, dt);
    }

    [Fact]
    public void Reader_UnixTimestampEpochZero()
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes("@0");
        var reader = new Utf8RdnReader(bytes);
        Assert.True(reader.Read());
        Assert.Equal(RdnTokenType.RdnDateTime, reader.TokenType);

        var dt = reader.GetRdnDateTime();
        Assert.Equal(DateTime.UnixEpoch, dt);
    }

    [Fact]
    public void Reader_LargeUnixTimestamp_Year2100()
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes("@4102444800");
        var reader = new Utf8RdnReader(bytes);
        Assert.True(reader.Read());
        Assert.Equal(RdnTokenType.RdnDateTime, reader.TokenType);

        var dt = reader.GetRdnDateTime();
        Assert.Equal(2100, dt.Year);
        Assert.Equal(1, dt.Month);
        Assert.Equal(1, dt.Day);
    }

    [Fact]
    public void Reader_DateTimeOffset_FromRdnDateTime()
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes("@2024-01-15T10:30:00.123Z");
        var reader = new Utf8RdnReader(bytes);
        Assert.True(reader.Read());
        Assert.Equal(RdnTokenType.RdnDateTime, reader.TokenType);

        Assert.True(reader.TryGetDateTimeOffset(out DateTimeOffset dto));
        Assert.Equal(new DateTimeOffset(2024, 1, 15, 10, 30, 0, 123, TimeSpan.Zero), dto);
    }

    #endregion

    #region 1b. Utf8RdnReader — RdnTimeOnly parsing

    [Fact]
    public void Reader_TimeOnly_Basic()
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes("@14:30:00");
        var reader = new Utf8RdnReader(bytes);
        Assert.True(reader.Read());
        Assert.Equal(RdnTokenType.RdnTimeOnly, reader.TokenType);

        var time = reader.GetRdnTimeOnly();
        Assert.Equal(new TimeOnly(14, 30, 0), time);
    }

    [Fact]
    public void Reader_TimeOnly_WithMilliseconds()
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes("@14:30:00.500");
        var reader = new Utf8RdnReader(bytes);
        Assert.True(reader.Read());
        Assert.Equal(RdnTokenType.RdnTimeOnly, reader.TokenType);

        var time = reader.GetRdnTimeOnly();
        Assert.Equal(new TimeOnly(14, 30, 0, 500), time);
    }

    [Fact]
    public void Reader_TimeOnly_Midnight()
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes("@00:00:00");
        var reader = new Utf8RdnReader(bytes);
        Assert.True(reader.Read());
        Assert.Equal(RdnTokenType.RdnTimeOnly, reader.TokenType);

        var time = reader.GetRdnTimeOnly();
        Assert.Equal(TimeOnly.MinValue, time);
    }

    [Fact]
    public void Reader_TimeOnly_EndOfDay()
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes("@23:59:59.999");
        var reader = new Utf8RdnReader(bytes);
        Assert.True(reader.Read());
        Assert.Equal(RdnTokenType.RdnTimeOnly, reader.TokenType);

        var time = reader.GetRdnTimeOnly();
        Assert.Equal(new TimeOnly(23, 59, 59, 999), time);
    }

    #endregion

    #region 1c. Utf8RdnReader — RdnDuration parsing

    [Fact]
    public void Reader_Duration_Full()
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes("@P1Y2M3DT4H5M6S");
        var reader = new Utf8RdnReader(bytes);
        Assert.True(reader.Read());
        Assert.Equal(RdnTokenType.RdnDuration, reader.TokenType);

        var duration = reader.GetRdnDuration();
        Assert.Equal("P1Y2M3DT4H5M6S", duration.Iso);
    }

    [Fact]
    public void Reader_Duration_HoursOnly()
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes("@PT4H30M");
        var reader = new Utf8RdnReader(bytes);
        Assert.True(reader.Read());
        Assert.Equal(RdnTokenType.RdnDuration, reader.TokenType);

        var duration = reader.GetRdnDuration();
        Assert.Equal("PT4H30M", duration.Iso);
    }

    [Fact]
    public void Reader_Duration_DaysOnly()
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes("@P30D");
        var reader = new Utf8RdnReader(bytes);
        Assert.True(reader.Read());
        Assert.Equal(RdnTokenType.RdnDuration, reader.TokenType);

        var duration = reader.GetRdnDuration();
        Assert.Equal("P30D", duration.Iso);
    }

    [Fact]
    public void Reader_Duration_Weeks()
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes("@P1W");
        var reader = new Utf8RdnReader(bytes);
        Assert.True(reader.Read());
        Assert.Equal(RdnTokenType.RdnDuration, reader.TokenType);

        var duration = reader.GetRdnDuration();
        Assert.Equal("P1W", duration.Iso);
    }

    #endregion

    #region 2. Reader — Mixed content in objects/arrays

    [Fact]
    public void Reader_ObjectWithDateTime()
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes("""{"created": @2024-01-15T10:30:00.000Z, "name": "test", "count": 42}""");
        var reader = new Utf8RdnReader(bytes);

        Assert.True(reader.Read()); // StartObject
        Assert.Equal(RdnTokenType.StartObject, reader.TokenType);

        Assert.True(reader.Read()); // PropertyName "created"
        Assert.Equal(RdnTokenType.PropertyName, reader.TokenType);
        Assert.Equal("created", reader.GetString());

        Assert.True(reader.Read()); // RdnDateTime
        Assert.Equal(RdnTokenType.RdnDateTime, reader.TokenType);
        var dt = reader.GetRdnDateTime();
        Assert.Equal(new DateTime(2024, 1, 15, 10, 30, 0, 0, DateTimeKind.Utc), dt);

        Assert.True(reader.Read()); // PropertyName "name"
        Assert.Equal(RdnTokenType.PropertyName, reader.TokenType);
        Assert.Equal("name", reader.GetString());

        Assert.True(reader.Read()); // String "test"
        Assert.Equal(RdnTokenType.String, reader.TokenType);
        Assert.Equal("test", reader.GetString());

        Assert.True(reader.Read()); // PropertyName "count"
        Assert.Equal(RdnTokenType.PropertyName, reader.TokenType);
        Assert.Equal("count", reader.GetString());

        Assert.True(reader.Read()); // Number 42
        Assert.Equal(RdnTokenType.Number, reader.TokenType);
        Assert.Equal(42, reader.GetInt32());

        Assert.True(reader.Read()); // EndObject
        Assert.Equal(RdnTokenType.EndObject, reader.TokenType);
    }

    [Fact]
    public void Reader_ArrayWithMixedTypes()
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes("""[@2024-01-15, "hello", 42, @14:30:00, @P1D, true, null]""");
        var reader = new Utf8RdnReader(bytes);

        Assert.True(reader.Read()); // StartArray
        Assert.Equal(RdnTokenType.StartArray, reader.TokenType);

        Assert.True(reader.Read()); // RdnDateTime
        Assert.Equal(RdnTokenType.RdnDateTime, reader.TokenType);

        Assert.True(reader.Read()); // String
        Assert.Equal(RdnTokenType.String, reader.TokenType);
        Assert.Equal("hello", reader.GetString());

        Assert.True(reader.Read()); // Number
        Assert.Equal(RdnTokenType.Number, reader.TokenType);
        Assert.Equal(42, reader.GetInt32());

        Assert.True(reader.Read()); // RdnTimeOnly
        Assert.Equal(RdnTokenType.RdnTimeOnly, reader.TokenType);
        var time = reader.GetRdnTimeOnly();
        Assert.Equal(new TimeOnly(14, 30, 0), time);

        Assert.True(reader.Read()); // RdnDuration
        Assert.Equal(RdnTokenType.RdnDuration, reader.TokenType);
        var duration = reader.GetRdnDuration();
        Assert.Equal("P1D", duration.Iso);

        Assert.True(reader.Read()); // True
        Assert.Equal(RdnTokenType.True, reader.TokenType);

        Assert.True(reader.Read()); // Null
        Assert.Equal(RdnTokenType.Null, reader.TokenType);

        Assert.True(reader.Read()); // EndArray
        Assert.Equal(RdnTokenType.EndArray, reader.TokenType);
    }

    [Fact]
    public void Reader_ObjectWithAllRdnTypes()
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes("""{"dt": @2024-06-15T08:00:00.000Z, "time": @09:30:00, "dur": @PT2H30M, "str": "value", "num": 3.14, "bool": false, "nil": null}""");
        var reader = new Utf8RdnReader(bytes);

        Assert.True(reader.Read()); // StartObject

        Assert.True(reader.Read()); // "dt"
        Assert.True(reader.Read()); // RdnDateTime
        Assert.Equal(RdnTokenType.RdnDateTime, reader.TokenType);

        Assert.True(reader.Read()); // "time"
        Assert.True(reader.Read()); // RdnTimeOnly
        Assert.Equal(RdnTokenType.RdnTimeOnly, reader.TokenType);

        Assert.True(reader.Read()); // "dur"
        Assert.True(reader.Read()); // RdnDuration
        Assert.Equal(RdnTokenType.RdnDuration, reader.TokenType);

        Assert.True(reader.Read()); // "str"
        Assert.True(reader.Read()); // String
        Assert.Equal(RdnTokenType.String, reader.TokenType);

        Assert.True(reader.Read()); // "num"
        Assert.True(reader.Read()); // Number
        Assert.Equal(RdnTokenType.Number, reader.TokenType);

        Assert.True(reader.Read()); // "bool"
        Assert.True(reader.Read()); // False
        Assert.Equal(RdnTokenType.False, reader.TokenType);

        Assert.True(reader.Read()); // "nil"
        Assert.True(reader.Read()); // Null
        Assert.Equal(RdnTokenType.Null, reader.TokenType);

        Assert.True(reader.Read()); // EndObject
    }

    #endregion

    #region 3. Writer tests

    [Fact]
    public void Writer_DateTime()
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8RdnWriter(stream))
        {
            writer.WriteRdnDateTimeValue(new DateTime(2024, 1, 15, 10, 30, 0, 0, DateTimeKind.Utc));
        }

        var output = System.Text.Encoding.UTF8.GetString(stream.ToArray());
        Assert.Equal("@2024-01-15T10:30:00.000Z", output);
    }

    [Fact]
    public void Writer_DateTime_WithMilliseconds()
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8RdnWriter(stream))
        {
            writer.WriteRdnDateTimeValue(new DateTime(2024, 1, 15, 10, 30, 0, 123, DateTimeKind.Utc));
        }

        var output = System.Text.Encoding.UTF8.GetString(stream.ToArray());
        Assert.Equal("@2024-01-15T10:30:00.123Z", output);
    }

    [Fact]
    public void Writer_DateTimeOffset()
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8RdnWriter(stream))
        {
            writer.WriteRdnDateTimeOffsetValue(new DateTimeOffset(2024, 3, 20, 15, 45, 30, 500, TimeSpan.Zero));
        }

        var output = System.Text.Encoding.UTF8.GetString(stream.ToArray());
        Assert.Equal("@2024-03-20T15:45:30.500Z", output);
    }

    [Fact]
    public void Writer_DateTimeOffset_NonUtc()
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8RdnWriter(stream))
        {
            // +05:30 offset: 2024-01-15T16:00:00+05:30 = 2024-01-15T10:30:00Z
            writer.WriteRdnDateTimeOffsetValue(new DateTimeOffset(2024, 1, 15, 16, 0, 0, TimeSpan.FromHours(5.5)));
        }

        var output = System.Text.Encoding.UTF8.GetString(stream.ToArray());
        Assert.Equal("@2024-01-15T10:30:00.000Z", output);
    }

    [Fact]
    public void Writer_TimeOnly_NoMilliseconds()
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8RdnWriter(stream))
        {
            writer.WriteRdnTimeOnlyValue(new TimeOnly(14, 30, 0));
        }

        var output = System.Text.Encoding.UTF8.GetString(stream.ToArray());
        Assert.Equal("@14:30:00", output);
    }

    [Fact]
    public void Writer_TimeOnly_WithMilliseconds()
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8RdnWriter(stream))
        {
            writer.WriteRdnTimeOnlyValue(new TimeOnly(14, 30, 0, 500));
        }

        var output = System.Text.Encoding.UTF8.GetString(stream.ToArray());
        Assert.Equal("@14:30:00.500", output);
    }

    [Fact]
    public void Writer_TimeOnly_Midnight()
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8RdnWriter(stream))
        {
            writer.WriteRdnTimeOnlyValue(TimeOnly.MinValue);
        }

        var output = System.Text.Encoding.UTF8.GetString(stream.ToArray());
        Assert.Equal("@00:00:00", output);
    }

    [Fact]
    public void Writer_Duration()
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8RdnWriter(stream))
        {
            writer.WriteRdnDurationValue(new RdnDuration("P1Y2M3DT4H5M6S"));
        }

        var output = System.Text.Encoding.UTF8.GetString(stream.ToArray());
        Assert.Equal("@P1Y2M3DT4H5M6S", output);
    }

    [Fact]
    public void Writer_Duration_Weeks()
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8RdnWriter(stream))
        {
            writer.WriteRdnDurationValue(new RdnDuration("P1W"));
        }

        var output = System.Text.Encoding.UTF8.GetString(stream.ToArray());
        Assert.Equal("@P1W", output);
    }

    [Fact]
    public void Writer_NoQuotes_DateTime()
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8RdnWriter(stream))
        {
            writer.WriteStartObject();
            writer.WritePropertyName("dt");
            writer.WriteRdnDateTimeValue(new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc));
            writer.WriteEndObject();
        }

        var output = System.Text.Encoding.UTF8.GetString(stream.ToArray());
        Assert.Contains("@2024-01-01T00:00:00.000Z", output);
        Assert.DoesNotContain("\"@", output);
    }

    [Fact]
    public void Writer_NoQuotes_TimeOnly()
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8RdnWriter(stream))
        {
            writer.WriteStartObject();
            writer.WritePropertyName("t");
            writer.WriteRdnTimeOnlyValue(new TimeOnly(12, 0, 0));
            writer.WriteEndObject();
        }

        var output = System.Text.Encoding.UTF8.GetString(stream.ToArray());
        Assert.Contains("@12:00:00", output);
        Assert.DoesNotContain("\"@", output);
    }

    [Fact]
    public void Writer_NoQuotes_Duration()
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8RdnWriter(stream))
        {
            writer.WriteStartObject();
            writer.WritePropertyName("d");
            writer.WriteRdnDurationValue(new RdnDuration("PT1H"));
            writer.WriteEndObject();
        }

        var output = System.Text.Encoding.UTF8.GetString(stream.ToArray());
        Assert.Contains("@PT1H", output);
        Assert.DoesNotContain("\"@", output);
    }

    [Fact]
    public void Writer_MultipleRdnValuesInArray()
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8RdnWriter(stream))
        {
            writer.WriteStartArray();
            writer.WriteRdnDateTimeValue(new DateTime(2024, 1, 15, 0, 0, 0, DateTimeKind.Utc));
            writer.WriteRdnTimeOnlyValue(new TimeOnly(10, 0, 0));
            writer.WriteRdnDurationValue(new RdnDuration("P1D"));
            writer.WriteEndArray();
        }

        var output = System.Text.Encoding.UTF8.GetString(stream.ToArray());
        Assert.Equal("[@2024-01-15T00:00:00.000Z,@10:00:00,@P1D]", output);
    }

    #endregion

    #region 4. Serialization roundtrip tests

    private record DateTimeRecord(DateTime Created, string Name);
    private record DateTimeOffsetRecord(DateTimeOffset Timestamp);
    private record TimeOnlyRecord(TimeOnly Start);
    private record DurationRecord(RdnDuration Length);
    private record MixedRecord(DateTime Created, TimeOnly StartTime, RdnDuration Duration, string Label, int Count);

    [Fact]
    public void Roundtrip_DateTime()
    {
        var original = new DateTimeRecord(new DateTime(2024, 1, 15, 10, 30, 0, DateTimeKind.Utc), "test");
        string rdn = RdnSerializer.Serialize(original);

        Assert.Contains("@2024-01-15T10:30:00.000Z", rdn);
        Assert.DoesNotContain("\"2024", rdn);

        var deserialized = RdnSerializer.Deserialize<DateTimeRecord>(rdn);
        Assert.NotNull(deserialized);
        Assert.Equal(original.Created, deserialized.Created);
        Assert.Equal(original.Name, deserialized.Name);
    }

    [Fact]
    public void Roundtrip_DateTimeOffset()
    {
        var original = new DateTimeOffsetRecord(new DateTimeOffset(2024, 6, 15, 12, 0, 0, TimeSpan.Zero));
        string rdn = RdnSerializer.Serialize(original);

        Assert.Contains("@2024-06-15T12:00:00.000Z", rdn);

        var deserialized = RdnSerializer.Deserialize<DateTimeOffsetRecord>(rdn);
        Assert.NotNull(deserialized);
        Assert.Equal(original.Timestamp, deserialized.Timestamp);
    }

    [Fact]
    public void Roundtrip_TimeOnly()
    {
        var original = new TimeOnlyRecord(new TimeOnly(14, 30, 0));
        string rdn = RdnSerializer.Serialize(original);

        Assert.Contains("@14:30:00", rdn);

        var deserialized = RdnSerializer.Deserialize<TimeOnlyRecord>(rdn);
        Assert.NotNull(deserialized);
        Assert.Equal(original.Start, deserialized.Start);
    }

    [Fact]
    public void Roundtrip_TimeOnly_WithMilliseconds()
    {
        var original = new TimeOnlyRecord(new TimeOnly(9, 15, 30, 250));
        string rdn = RdnSerializer.Serialize(original);

        Assert.Contains("@09:15:30.250", rdn);

        var deserialized = RdnSerializer.Deserialize<TimeOnlyRecord>(rdn);
        Assert.NotNull(deserialized);
        Assert.Equal(original.Start, deserialized.Start);
    }

    [Fact]
    public void Roundtrip_Duration()
    {
        var original = new DurationRecord(new RdnDuration("P1Y2M3DT4H5M6S"));
        string rdn = RdnSerializer.Serialize(original);

        Assert.Contains("@P1Y2M3DT4H5M6S", rdn);

        var deserialized = RdnSerializer.Deserialize<DurationRecord>(rdn);
        Assert.NotNull(deserialized);
        Assert.Equal(original.Length, deserialized.Length);
    }

    [Fact]
    public void Roundtrip_MixedRecord()
    {
        var original = new MixedRecord(new DateTime(2024, 1, 15, 10, 30, 0, DateTimeKind.Utc), new TimeOnly(9, 0, 0), new RdnDuration("PT2H"), "meeting", 5);
        string rdn = RdnSerializer.Serialize(original);

        Assert.Contains("@2024-01-15T10:30:00.000Z", rdn);
        Assert.Contains("@09:00:00", rdn);
        Assert.Contains("@PT2H", rdn);
        Assert.Contains("\"meeting\"", rdn);
        Assert.Contains("5", rdn);

        var deserialized = RdnSerializer.Deserialize<MixedRecord>(rdn);
        Assert.NotNull(deserialized);
        Assert.Equal(original.Created, deserialized.Created);
        Assert.Equal(original.StartTime, deserialized.StartTime);
        Assert.Equal(original.Duration, deserialized.Duration);
        Assert.Equal(original.Label, deserialized.Label);
        Assert.Equal(original.Count, deserialized.Count);
    }

    [Fact]
    public void Serialization_DateTime_CamelCase()
    {
        var options = new RdnSerializerOptions { PropertyNamingPolicy = RdnNamingPolicy.CamelCase };
        var original = new DateTimeRecord(new DateTime(2024, 1, 15, 10, 30, 0, DateTimeKind.Utc), "test");
        string rdn = RdnSerializer.Serialize(original, options);

        Assert.Contains("\"created\":", rdn);
        Assert.Contains("@2024-01-15T10:30:00.000Z", rdn);

        var deserialized = RdnSerializer.Deserialize<DateTimeRecord>(rdn, options);
        Assert.NotNull(deserialized);
        Assert.Equal(original.Created, deserialized.Created);
    }

    #endregion

    #region 5. RdnDocument tests

    [Fact]
    public void Document_DateTime()
    {
        using var doc = RdnDocument.Parse("""{"dt": @2024-01-15T10:30:00.000Z}""");
        var elem = doc.RootElement.GetProperty("dt");
        Assert.Equal(RdnValueKind.RdnDateTime, elem.ValueKind);

        var dt = elem.GetDateTime();
        Assert.Equal(new DateTime(2024, 1, 15, 10, 30, 0, 0, DateTimeKind.Utc), dt);
    }

    [Fact]
    public void Document_DateTimeOffset()
    {
        using var doc = RdnDocument.Parse("""{"dto": @2024-06-15T12:00:00.000Z}""");
        var elem = doc.RootElement.GetProperty("dto");
        Assert.Equal(RdnValueKind.RdnDateTime, elem.ValueKind);

        var dto = elem.GetDateTimeOffset();
        Assert.Equal(new DateTimeOffset(2024, 6, 15, 12, 0, 0, 0, TimeSpan.Zero), dto);
    }

    [Fact]
    public void Document_TimeOnly()
    {
        using var doc = RdnDocument.Parse("""{"t": @14:30:00}""");
        var elem = doc.RootElement.GetProperty("t");
        Assert.Equal(RdnValueKind.RdnTimeOnly, elem.ValueKind);

        var time = elem.GetRdnTimeOnly();
        Assert.Equal(new TimeOnly(14, 30, 0), time);
    }

    [Fact]
    public void Document_Duration()
    {
        using var doc = RdnDocument.Parse("""{"d": @P1Y2M3DT4H5M6S}""");
        var elem = doc.RootElement.GetProperty("d");
        Assert.Equal(RdnValueKind.RdnDuration, elem.ValueKind);

        var duration = elem.GetRdnDuration();
        Assert.Equal("P1Y2M3DT4H5M6S", duration.Iso);
    }

    [Fact]
    public void Document_UnixTimestamp()
    {
        using var doc = RdnDocument.Parse("""{"ts": @1705312200}""");
        var elem = doc.RootElement.GetProperty("ts");
        Assert.Equal(RdnValueKind.RdnDateTime, elem.ValueKind);

        var dt = elem.GetDateTime();
        Assert.Equal(DateTime.UnixEpoch.AddSeconds(1705312200), dt);
    }

    [Fact]
    public void Document_MixedContent()
    {
        using var doc = RdnDocument.Parse("""{"dt": @2024-01-15T00:00:00.000Z, "t": @09:30:00, "d": @PT1H, "s": "hello", "n": 42}""");

        Assert.Equal(RdnValueKind.RdnDateTime, doc.RootElement.GetProperty("dt").ValueKind);
        Assert.Equal(RdnValueKind.RdnTimeOnly, doc.RootElement.GetProperty("t").ValueKind);
        Assert.Equal(RdnValueKind.RdnDuration, doc.RootElement.GetProperty("d").ValueKind);
        Assert.Equal(RdnValueKind.String, doc.RootElement.GetProperty("s").ValueKind);
        Assert.Equal(RdnValueKind.Number, doc.RootElement.GetProperty("n").ValueKind);
    }

    [Fact]
    public void Document_ArrayWithRdnValues()
    {
        using var doc = RdnDocument.Parse("""[@2024-01-15T00:00:00.000Z, @14:30:00, @P1D]""");
        var arr = doc.RootElement;
        Assert.Equal(RdnValueKind.Array, arr.ValueKind);
        Assert.Equal(3, arr.GetArrayLength());

        Assert.Equal(RdnValueKind.RdnDateTime, arr[0].ValueKind);
        Assert.Equal(RdnValueKind.RdnTimeOnly, arr[1].ValueKind);
        Assert.Equal(RdnValueKind.RdnDuration, arr[2].ValueKind);
    }

    #endregion

    #region 6. RdnDuration struct tests

    [Fact]
    public void RdnDuration_Constructor()
    {
        var d = new RdnDuration("P1Y2M3DT4H5M6S");
        Assert.Equal("P1Y2M3DT4H5M6S", d.Iso);
    }

    [Fact]
    public void RdnDuration_ToString()
    {
        var d = new RdnDuration("PT4H30M");
        Assert.Equal("PT4H30M", d.ToString());
    }

    [Fact]
    public void RdnDuration_ToString_Default()
    {
        var d = default(RdnDuration);
        Assert.Equal("", d.ToString());
    }

    [Fact]
    public void RdnDuration_NullThrows()
    {
        Assert.Throws<ArgumentNullException>(() => new RdnDuration(null!));
    }

    [Fact]
    public void RdnDuration_TryToTimeSpan_Succeeds_PT4H5M6S()
    {
        var d = new RdnDuration("PT4H5M6S");
        Assert.True(d.TryToTimeSpan(out TimeSpan ts));
        Assert.Equal(new TimeSpan(0, 4, 5, 6), ts);
    }

    [Fact]
    public void RdnDuration_TryToTimeSpan_Succeeds_P30D()
    {
        var d = new RdnDuration("P30D");
        Assert.True(d.TryToTimeSpan(out TimeSpan ts));
        Assert.Equal(TimeSpan.FromDays(30), ts);
    }

    [Fact]
    public void RdnDuration_TryToTimeSpan_Succeeds_P1W()
    {
        var d = new RdnDuration("P1W");
        Assert.True(d.TryToTimeSpan(out TimeSpan ts));
        Assert.Equal(TimeSpan.FromDays(7), ts);
    }

    [Fact]
    public void RdnDuration_TryToTimeSpan_Fails_WithYears()
    {
        var d = new RdnDuration("P1Y2M3D");
        Assert.False(d.TryToTimeSpan(out _));
    }

    [Fact]
    public void RdnDuration_TryToTimeSpan_Fails_WithMonths()
    {
        var d = new RdnDuration("P2M");
        Assert.False(d.TryToTimeSpan(out _));
    }

    [Fact]
    public void RdnDuration_Equality()
    {
        var a = new RdnDuration("PT1H");
        var b = new RdnDuration("PT1H");
        var c = new RdnDuration("PT2H");

        Assert.True(a == b);
        Assert.True(a.Equals(b));
        Assert.False(a == c);
        Assert.True(a != c);
        Assert.False(a.Equals(c));
    }

    [Fact]
    public void RdnDuration_Equality_Object()
    {
        var a = new RdnDuration("PT1H");
        object b = new RdnDuration("PT1H");
        object c = "not a duration";

        Assert.True(a.Equals(b));
        Assert.False(a.Equals(c));
    }

    [Fact]
    public void RdnDuration_GetHashCode_Equal()
    {
        var a = new RdnDuration("P1D");
        var b = new RdnDuration("P1D");
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void RdnDuration_GetHashCode_Default()
    {
        var d = default(RdnDuration);
        Assert.Equal(0, d.GetHashCode());
    }

    #endregion

    #region 7. Edge cases

    [Fact]
    public void Reader_DateTimeAtEndOfInput()
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes("@2024-12-31T23:59:59.999Z");
        var reader = new Utf8RdnReader(bytes);
        Assert.True(reader.Read());
        Assert.Equal(RdnTokenType.RdnDateTime, reader.TokenType);

        var dt = reader.GetRdnDateTime();
        Assert.Equal(new DateTime(2024, 12, 31, 23, 59, 59, 999, DateTimeKind.Utc), dt);
    }

    [Fact]
    public void Reader_DurationAtEndOfArray()
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes("[1,@PT1H]");
        var reader = new Utf8RdnReader(bytes);

        Assert.True(reader.Read()); // StartArray
        Assert.True(reader.Read()); // Number 1
        Assert.True(reader.Read()); // RdnDuration
        Assert.Equal(RdnTokenType.RdnDuration, reader.TokenType);
        Assert.Equal("PT1H", reader.GetRdnDuration().Iso);

        Assert.True(reader.Read()); // EndArray
    }

    [Fact]
    public void Writer_RdnDateTime_InObject()
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8RdnWriter(stream))
        {
            writer.WriteStartObject();
            writer.WritePropertyName("created");
            writer.WriteRdnDateTimeValue(new DateTime(2024, 1, 15, 0, 0, 0, DateTimeKind.Utc));
            writer.WritePropertyName("updated");
            writer.WriteRdnDateTimeValue(new DateTime(2024, 6, 15, 12, 0, 0, DateTimeKind.Utc));
            writer.WriteEndObject();
        }

        var output = System.Text.Encoding.UTF8.GetString(stream.ToArray());
        Assert.Contains("\"created\":@2024-01-15T00:00:00.000Z", output);
        Assert.Contains("\"updated\":@2024-06-15T12:00:00.000Z", output);
    }

    [Fact]
    public void Reader_ConsecutiveDateTimesInArray()
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes("[@2024-01-01T00:00:00.000Z,@2024-12-31T23:59:59.000Z]");
        var reader = new Utf8RdnReader(bytes);

        Assert.True(reader.Read()); // StartArray
        Assert.True(reader.Read()); // First RdnDateTime
        Assert.Equal(RdnTokenType.RdnDateTime, reader.TokenType);
        var dt1 = reader.GetRdnDateTime();
        Assert.Equal(new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc), dt1);

        Assert.True(reader.Read()); // Second RdnDateTime
        Assert.Equal(RdnTokenType.RdnDateTime, reader.TokenType);
        var dt2 = reader.GetRdnDateTime();
        Assert.Equal(new DateTime(2024, 12, 31, 23, 59, 59, DateTimeKind.Utc), dt2);

        Assert.True(reader.Read()); // EndArray
    }

    [Fact]
    public void Reader_ValueSpan_ExcludesAtSign()
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes("@2024-01-15T10:30:00.123Z");
        var reader = new Utf8RdnReader(bytes);
        Assert.True(reader.Read());
        Assert.Equal(RdnTokenType.RdnDateTime, reader.TokenType);

        // ValueSpan should contain the body after @, not the @ itself
        var span = reader.ValueSpan;
        var body = System.Text.Encoding.UTF8.GetString(span);
        Assert.Equal("2024-01-15T10:30:00.123Z", body);
        Assert.DoesNotContain("@", body);
    }

    [Fact]
    public void Reader_TimeOnly_ValueSpan_ExcludesAtSign()
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes("@14:30:00");
        var reader = new Utf8RdnReader(bytes);
        Assert.True(reader.Read());

        var body = System.Text.Encoding.UTF8.GetString(reader.ValueSpan);
        Assert.Equal("14:30:00", body);
    }

    [Fact]
    public void Reader_Duration_ValueSpan_ExcludesAtSign()
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes("@P1Y2M3DT4H5M6S");
        var reader = new Utf8RdnReader(bytes);
        Assert.True(reader.Read());

        var body = System.Text.Encoding.UTF8.GetString(reader.ValueSpan);
        Assert.Equal("P1Y2M3DT4H5M6S", body);
    }

    #endregion

    #region 7b. DateOnly serialization

    private record DateOnlyRecord(DateOnly Date, string Label);

    [Fact]
    public void Roundtrip_DateOnly()
    {
        var original = new DateOnlyRecord(new DateOnly(2024, 6, 15), "release");
        string rdn = RdnSerializer.Serialize(original);

        Assert.Contains("@2024-06-15", rdn);
        Assert.DoesNotContain("\"2024-06-15\"", rdn);

        var deserialized = RdnSerializer.Deserialize<DateOnlyRecord>(rdn);
        Assert.NotNull(deserialized);
        Assert.Equal(original.Date, deserialized.Date);
        Assert.Equal(original.Label, deserialized.Label);
    }

    [Fact]
    public void Writer_DateOnly()
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8RdnWriter(stream))
        {
            writer.WriteRdnDateOnlyValue(new DateOnly(2024, 1, 15));
        }

        var output = System.Text.Encoding.UTF8.GetString(stream.ToArray());
        Assert.Equal("@2024-01-15", output);
    }

    [Fact]
    public void Writer_DateOnly_InObject()
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8RdnWriter(stream))
        {
            writer.WriteStartObject();
            writer.WritePropertyName("date");
            writer.WriteRdnDateOnlyValue(new DateOnly(2024, 3, 20));
            writer.WriteEndObject();
        }

        var output = System.Text.Encoding.UTF8.GetString(stream.ToArray());
        Assert.Contains("\"date\":@2024-03-20", output);
        Assert.DoesNotContain("\"@", output);
    }

    [Fact]
    public void Reader_DateOnly_FromRdnDateTime()
    {
        var rdn = """{"Date": @2024-01-15, "Label": "test"}""";
        var deserialized = RdnSerializer.Deserialize<DateOnlyRecord>(rdn);
        Assert.NotNull(deserialized);
        Assert.Equal(new DateOnly(2024, 1, 15), deserialized.Date);
    }

    [Fact]
    public void Reader_DateOnly_FromRdnDateTimeWithTime()
    {
        // When a full datetime RDN literal is deserialized into DateOnly, should extract the date part
        var rdn = """{"Date": @2024-06-15T10:30:00.000Z, "Label": "test"}""";
        var deserialized = RdnSerializer.Deserialize<DateOnlyRecord>(rdn);
        Assert.NotNull(deserialized);
        Assert.Equal(new DateOnly(2024, 6, 15), deserialized.Date);
    }

    #endregion

    #region 7c. TimeSpan serialization

    private record TimeSpanRecord(TimeSpan Duration, string Label);

    [Fact]
    public void Roundtrip_TimeSpan()
    {
        var original = new TimeSpanRecord(new TimeSpan(1, 2, 30, 0), "task");
        string rdn = RdnSerializer.Serialize(original);

        Assert.Contains("@P1DT2H30M", rdn);
        Assert.DoesNotContain("\"1.", rdn);

        var deserialized = RdnSerializer.Deserialize<TimeSpanRecord>(rdn);
        Assert.NotNull(deserialized);
        Assert.Equal(original.Duration, deserialized.Duration);
        Assert.Equal(original.Label, deserialized.Label);
    }

    [Fact]
    public void Roundtrip_TimeSpan_HoursOnly()
    {
        var original = new TimeSpanRecord(TimeSpan.FromHours(4), "meeting");
        string rdn = RdnSerializer.Serialize(original);

        Assert.Contains("@PT4H", rdn);

        var deserialized = RdnSerializer.Deserialize<TimeSpanRecord>(rdn);
        Assert.NotNull(deserialized);
        Assert.Equal(original.Duration, deserialized.Duration);
    }

    [Fact]
    public void Roundtrip_TimeSpan_Zero()
    {
        var original = new TimeSpanRecord(TimeSpan.Zero, "none");
        string rdn = RdnSerializer.Serialize(original);

        Assert.Contains("@P0D", rdn);

        var deserialized = RdnSerializer.Deserialize<TimeSpanRecord>(rdn);
        Assert.NotNull(deserialized);
        Assert.Equal(TimeSpan.Zero, deserialized.Duration);
    }

    [Fact]
    public void Roundtrip_TimeSpan_WithSeconds()
    {
        var original = new TimeSpanRecord(new TimeSpan(0, 0, 45), "quick");
        string rdn = RdnSerializer.Serialize(original);

        Assert.Contains("@PT45S", rdn);

        var deserialized = RdnSerializer.Deserialize<TimeSpanRecord>(rdn);
        Assert.NotNull(deserialized);
        Assert.Equal(original.Duration, deserialized.Duration);
    }

    [Fact]
    public void Roundtrip_TimeSpan_WithMilliseconds()
    {
        var original = new TimeSpanRecord(new TimeSpan(0, 0, 0, 1, 500), "precise");
        string rdn = RdnSerializer.Serialize(original);

        Assert.Contains("@PT1.5S", rdn);

        var deserialized = RdnSerializer.Deserialize<TimeSpanRecord>(rdn);
        Assert.NotNull(deserialized);
        Assert.Equal(original.Duration, deserialized.Duration);
    }

    [Fact]
    public void Writer_TimeSpan()
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8RdnWriter(stream))
        {
            writer.WriteRdnTimeSpanValue(new TimeSpan(2, 3, 4, 5));
        }

        var output = System.Text.Encoding.UTF8.GetString(stream.ToArray());
        Assert.Equal("@P2DT3H4M5S", output);
    }

    [Fact]
    public void Writer_TimeSpan_DaysOnly()
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8RdnWriter(stream))
        {
            writer.WriteRdnTimeSpanValue(TimeSpan.FromDays(30));
        }

        var output = System.Text.Encoding.UTF8.GetString(stream.ToArray());
        Assert.Equal("@P30D", output);
    }

    #endregion

    #region 8. Error cases

    [Fact]
    public void Reader_StandaloneAtSign_Throws()
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes("@");
        RdnException? caught = null;
        try
        {
            var reader = new Utf8RdnReader(bytes, isFinalBlock: true, state: default);
            reader.Read();
        }
        catch (RdnException ex)
        {
            caught = ex;
        }
        Assert.NotNull(caught);
    }

    [Fact]
    public void Reader_InvalidAfterAtSign_Throws()
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes("@XYZ");
        RdnException? caught = null;
        try
        {
            var reader = new Utf8RdnReader(bytes, isFinalBlock: true, state: default);
            reader.Read();
        }
        catch (RdnException ex)
        {
            caught = ex;
        }
        Assert.NotNull(caught);
    }

    [Fact]
    public void Reader_GetRdnDateTime_WrongTokenType_Throws()
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes("42");
        InvalidOperationException? caught = null;
        try
        {
            var reader = new Utf8RdnReader(bytes);
            reader.Read();
            reader.GetRdnDateTime();
        }
        catch (InvalidOperationException ex)
        {
            caught = ex;
        }
        Assert.NotNull(caught);
    }

    [Fact]
    public void Reader_GetRdnTimeOnly_WrongTokenType_Throws()
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes("42");
        InvalidOperationException? caught = null;
        try
        {
            var reader = new Utf8RdnReader(bytes);
            reader.Read();
            reader.GetRdnTimeOnly();
        }
        catch (InvalidOperationException ex)
        {
            caught = ex;
        }
        Assert.NotNull(caught);
    }

    [Fact]
    public void Reader_GetRdnDuration_WrongTokenType_Throws()
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes("42");
        InvalidOperationException? caught = null;
        try
        {
            var reader = new Utf8RdnReader(bytes);
            reader.Read();
            reader.GetRdnDuration();
        }
        catch (InvalidOperationException ex)
        {
            caught = ex;
        }
        Assert.NotNull(caught);
    }

    #endregion
}

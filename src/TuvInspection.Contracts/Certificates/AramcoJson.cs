using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TuvInspection.Contracts.Certificates;

/// <summary>
/// Shared, fault-tolerant JSON options for (de)serialising <see cref="AramcoReportData"/>.
///
/// The Annex-1 form posts several fields as free text (e.g. Inspection Time). A value that
/// does not parse as a <see cref="TimeOnly"/>/<see cref="DateOnly"/> must degrade to
/// <c>null</c> rather than throw — otherwise a single bad field makes the whole report
/// deserialise as empty and every value disappears from the generated PDF.
/// </summary>
public static class AramcoJson
{
    public static readonly JsonSerializerOptions Options = Build();

    private static JsonSerializerOptions Build()
    {
        var o = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = true,
        };
        o.Converters.Add(new LenientNullableTimeOnlyConverter());
        o.Converters.Add(new LenientNullableDateOnlyConverter());
        return o;
    }

    private sealed class LenientNullableTimeOnlyConverter : JsonConverter<TimeOnly?>
    {
        public override TimeOnly? Read(ref Utf8JsonReader reader, Type t, JsonSerializerOptions o)
        {
            if (reader.TokenType == JsonTokenType.Null) return null;
            if (reader.TokenType == JsonTokenType.String)
            {
                var s = reader.GetString();
                return !string.IsNullOrWhiteSpace(s)
                    && TimeOnly.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out var v)
                    ? v : null;
            }
            reader.Skip();
            return null;
        }

        public override void Write(Utf8JsonWriter writer, TimeOnly? value, JsonSerializerOptions o)
        {
            if (value is null) writer.WriteNullValue();
            else writer.WriteStringValue(value.Value.ToString("HH:mm:ss", CultureInfo.InvariantCulture));
        }
    }

    private sealed class LenientNullableDateOnlyConverter : JsonConverter<DateOnly?>
    {
        public override DateOnly? Read(ref Utf8JsonReader reader, Type t, JsonSerializerOptions o)
        {
            if (reader.TokenType == JsonTokenType.Null) return null;
            if (reader.TokenType == JsonTokenType.String)
            {
                var s = reader.GetString();
                return !string.IsNullOrWhiteSpace(s)
                    && DateOnly.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out var v)
                    ? v : null;
            }
            reader.Skip();
            return null;
        }

        public override void Write(Utf8JsonWriter writer, DateOnly? value, JsonSerializerOptions o)
        {
            if (value is null) writer.WriteNullValue();
            else writer.WriteStringValue(value.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        }
    }
}

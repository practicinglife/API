using System.Text.Json;

namespace MspTools.Connectors;

/// <summary>Convenience extension methods for safe JSON element property extraction.</summary>
internal static class JsonElementExtensions
{
    public static string TryGetString(this JsonElement el, string property)
    {
        if (el.TryGetProperty(property, out var v) && v.ValueKind == JsonValueKind.String)
            return v.GetString() ?? string.Empty;
        if (el.TryGetProperty(property, out v) && v.ValueKind == JsonValueKind.Number)
            return v.GetRawText();
        return string.Empty;
    }

    public static string TryGetNestedString(this JsonElement el, string property, string childProperty)
    {
        if (el.TryGetProperty(property, out var nested) && nested.ValueKind == JsonValueKind.Object)
            return nested.TryGetString(childProperty);
        return string.Empty;
    }

    /// <summary>Gets a string from a nested array element by zero-based index.</summary>
    public static string TryGetNestedString(this JsonElement el, string property, int arrayIndex)
    {
        if (el.TryGetProperty(property, out var nested) && nested.ValueKind == JsonValueKind.Array)
        {
            int i = 0;
            foreach (var item in nested.EnumerateArray())
            {
                if (i == arrayIndex)
                    return item.ValueKind == JsonValueKind.String ? item.GetString() ?? string.Empty : item.GetRawText();
                i++;
            }
        }
        return string.Empty;
    }

    public static bool? TryGetBool(this JsonElement el, string property)
    {
        if (el.TryGetProperty(property, out var v) && v.ValueKind == JsonValueKind.True) return true;
        if (el.TryGetProperty(property, out v) && v.ValueKind == JsonValueKind.False) return false;
        return null;
    }

    public static DateTime? TryGetDateTime(this JsonElement el, string property)
    {
        if (el.TryGetProperty(property, out var v) && v.ValueKind == JsonValueKind.String)
            if (DateTime.TryParse(v.GetString(), out var dt))
                return dt.ToUniversalTime();
        return null;
    }
}

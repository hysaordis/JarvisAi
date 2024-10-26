using System.Collections;
using System.ComponentModel;
using System.Reflection;

namespace Jarvis.Ai.Features.StarkArsenal.ModuleAttributes;

[AttributeUsage(AttributeTargets.Property)]
public class TacticalComponentAttribute : Attribute
{
    public string Description { get; }
    public string Type { get; }
    public bool IsRequired { get; }

    public TacticalComponentAttribute(string description, string type, bool isRequired = false)
    {
        Description = description;
        Type = type;
        IsRequired = isRequired;
    }
}
public static class ParameterExtractionHelper
{
    private class CaseInsensitiveKeyComparer : IEqualityComparer<string>
    {
        public bool Equals(string x, string y)
        {
            if (x == null && y == null) return true;
            if (x == null || y == null) return false;
            return NormalizeKey(x).Equals(NormalizeKey(y), StringComparison.OrdinalIgnoreCase);
        }

        public int GetHashCode(string obj)
        {
            if (obj == null) return 0;
            return NormalizeKey(obj).ToLowerInvariant().GetHashCode();
        }
    }

    public static void ExtractAndSetParameters<T>(T instance, Dictionary<string, object> originalArgs) where T : class
    {
        // Create a new dictionary with case-insensitive, normalized keys
        var args = new Dictionary<string, object>(originalArgs, new CaseInsensitiveKeyComparer());

        var properties = instance.GetType()
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.GetCustomAttribute<TacticalComponentAttribute>() != null);

        foreach (var property in properties)
        {
            var attribute = property.GetCustomAttribute<TacticalComponentAttribute>();
            var paramName = GetParameterName(property.Name);

            // Try to find the matching key using various formats
            var matchingKey = FindMatchingKey(args, paramName);

            if (matchingKey == null)
            {
                if (attribute.IsRequired)
                {
                    // Log available keys for debugging
                    var availableKeys = string.Join(", ", args.Keys.Select(k => $"'{k}'"));
                    throw new ArgumentException(
                        $"Required parameter '{paramName}' was not provided. Available keys: {availableKeys}");
                }
                continue;
            }

            try
            {
                var value = args[matchingKey];
                var convertedValue = ConvertValue(value, property.PropertyType);
                property.SetValue(instance, convertedValue);
            }
            catch (Exception ex)
            {
                throw new ArgumentException(
                    $"Failed to set property '{property.Name}' with value '{args[matchingKey]}'. Expected type: {property.PropertyType.Name}",
                    ex);
            }
        }
    }

    private static string NormalizeKey(string key)
    {
        if (string.IsNullOrEmpty(key)) return key;

        // Remove leading/trailing spaces and convert to lowercase
        key = key.Trim().ToLowerInvariant();

        // Remove extra spaces between words
        key = string.Join("_", key.Split(new[] { ' ', '_' }, StringSplitOptions.RemoveEmptyEntries));

        return key;
    }

    private static string GetParameterName(string propertyName)
    {
        // Convert PascalCase to snake_case
        return string.Concat(propertyName.Select((x, i) => i > 0 && char.IsUpper(x) ? "_" + x.ToString() : x.ToString()))
            .ToLower();
    }

    private static string FindMatchingKey(Dictionary<string, object> args, string paramName)
    {
        // Lista di possibili varianti del nome del parametro
        var variations = new[]
        {
            paramName,                        // original
            paramName.ToLowerInvariant(),     // lowercase
            paramName.ToUpperInvariant(),     // uppercase
            NormalizeKey(paramName),          // normalized
            paramName.Replace("_", ""),       // no underscores
            paramName.Replace("_", " "),      // spaces instead of underscores
        }.Distinct();

        foreach (var variation in variations)
        {
            var matchingKey = args.Keys.FirstOrDefault(k =>
                NormalizeKey(k).Equals(NormalizeKey(variation), StringComparison.OrdinalIgnoreCase));

            if (matchingKey != null)
            {
                return matchingKey;
            }
        }

        // Try fuzzy matching if exact match not found
        var bestMatch = args.Keys
            .Select(k => new { Key = k, Distance = ComputeLevenshteinDistance(NormalizeKey(k), NormalizeKey(paramName)) })
            .Where(x => x.Distance <= 2) // Allow up to 2 character differences
            .OrderBy(x => x.Distance)
            .FirstOrDefault();

        return bestMatch?.Key;
    }

    private static object ConvertValue(object value, Type targetType)
    {
        try
        {
            if (value == null)
            {
                return targetType.IsValueType ? Activator.CreateInstance(targetType) : null;
            }

            // Handle string conversion first
            if (targetType == typeof(string))
            {
                return value.ToString();
            }

            // Handle nullable types
            if (targetType.IsGenericType && targetType.GetGenericTypeDefinition() == typeof(Nullable<>))
            {
                if (value == null) return null;
                targetType = Nullable.GetUnderlyingType(targetType);
            }

            // Handle arrays
            if (targetType.IsArray)
            {
                return ConvertArray(value, targetType);
            }

            // Handle enums
            if (targetType.IsEnum)
            {
                if (value is string strValue)
                {
                    return Enum.Parse(targetType, strValue, true);
                }
                return Enum.ToObject(targetType, value);
            }

            // Handle common primitive types
            if (targetType == typeof(bool))
            {
                if (value is string strValue)
                {
                    return bool.Parse(strValue);
                }
                return Convert.ToBoolean(value);
            }

            if (targetType == typeof(int))
            {
                return Convert.ToInt32(value);
            }

            if (targetType == typeof(long))
            {
                return Convert.ToInt64(value);
            }

            if (targetType == typeof(double))
            {
                return Convert.ToDouble(value);
            }

            if (targetType == typeof(decimal))
            {
                return Convert.ToDecimal(value);
            }

            if (targetType == typeof(DateTime))
            {
                if (value is string strValue)
                {
                    return DateTime.Parse(strValue);
                }
                return Convert.ToDateTime(value);
            }

            // Try using TypeConverter for more complex types
            var converter = TypeDescriptor.GetConverter(targetType);
            if (converter.CanConvertFrom(value.GetType()))
            {
                return converter.ConvertFrom(value);
            }

            // Last resort: try direct conversion
            return Convert.ChangeType(value, targetType);
        }
        catch (Exception ex)
        {
            throw new ArgumentException($"Failed to convert value '{value}' to type {targetType.Name}", ex);
        }
    }

    private static object ConvertArray(object value, Type targetType)
    {
        if (value is not IEnumerable<object> enumerable)
        {
            if (value is IEnumerable enumObj)
            {
                enumerable = enumObj.Cast<object>();
            }
            else
            {
                throw new ArgumentException($"Cannot convert non-enumerable value to array type {targetType.Name}");
            }
        }

        var elementType = targetType.GetElementType();
        var list = enumerable.Select(item => ConvertValue(item, elementType)).ToList();
        var array = Array.CreateInstance(elementType, list.Count);

        for (int i = 0; i < list.Count; i++)
        {
            array.SetValue(list[i], i);
        }

        return array;
    }

    private static int ComputeLevenshteinDistance(string s1, string s2)
    {
        if (string.IsNullOrEmpty(s1))
        {
            return string.IsNullOrEmpty(s2) ? 0 : s2.Length;
        }

        if (string.IsNullOrEmpty(s2))
        {
            return s1.Length;
        }

        var distances = new int[s1.Length + 1, s2.Length + 1];

        for (int i = 0; i <= s1.Length; i++)
            distances[i, 0] = i;

        for (int j = 0; j <= s2.Length; j++)
            distances[0, j] = j;

        for (int i = 1; i <= s1.Length; i++)
            for (int j = 1; j <= s2.Length; j++)
            {
                int cost = (s1[i - 1] == s2[j - 1]) ? 0 : 1;
                distances[i, j] = Math.Min(
                    Math.Min(distances[i - 1, j] + 1, distances[i, j - 1] + 1),
                    distances[i - 1, j - 1] + cost);
            }

        return distances[s1.Length, s2.Length];
    }
}
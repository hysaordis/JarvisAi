using System.Diagnostics;
using System.Text.Json;
using Jarvis.Ai.Common.Settings;

namespace Jarvis.Ai.Common.Utils;

public static class Timeit
{
    public static async Task<T> MeasureAsync<T>(Func<Task<T>> func, string functionName)
    {
        var stopwatch = Stopwatch.StartNew();
        T result = await func();
        stopwatch.Stop();
        double duration = stopwatch.Elapsed.TotalSeconds;
        Console.WriteLine($"⏰ {functionName}() took {duration:F4} seconds");

        var timeRecord = new
        {
            timestamp = DateTime.Now.ToString("o"),
            function = functionName,
            duration = duration.ToString("F4")
        };

        await using var file = new StreamWriter(Constants.RUN_TIME_TABLE_LOG_JSON, true);
        string json = JsonSerializer.Serialize(timeRecord);
        await file.WriteLineAsync(json);

        return result;
    }

    public static T Measure<T>(Func<T> func, string functionName)
    {
        var stopwatch = Stopwatch.StartNew();
        T result = func();
        stopwatch.Stop();
        double duration = stopwatch.Elapsed.TotalSeconds;
        Console.WriteLine($"⏰ {functionName}() took {duration:F4} seconds");

        var timeRecord = new
        {
            timestamp = DateTime.Now.ToString("o"),
            function = functionName,
            duration = duration.ToString("F4")
        };

        using (var file = new StreamWriter(Constants.RUN_TIME_TABLE_LOG_JSON, true))
        {
            string json = JsonSerializer.Serialize(timeRecord);
            file.WriteLine(json);
        }

        return result;
    }
}

public static class Utils
{
    public static bool MatchPattern(string pattern, string key)
    {
        if (pattern == "*")
        {
            return true;
        }
        else if (pattern.StartsWith("*") && pattern.EndsWith("*"))
        {
            return key.Contains(pattern.Trim('*'));
        }
        else if (pattern.StartsWith("*"))
        {
            return key.EndsWith(pattern.TrimStart('*'));
        }
        else if (pattern.EndsWith("*"))
        {
            return key.StartsWith(pattern.TrimEnd('*'));
        }
        else
        {
            return pattern == key;
        }
    }
}
using Jarvis.Ai.Models;

namespace Jarvis.Ai.Common.Settings;

public static class Constants
{
    public const string RUN_TIME_TABLE_LOG_JSON = "runtime_time_table.json";
    public const int CHUNK = 1024;
    public const int CHANNELS = 1;
    public const int RATE = 24000;
    public const int BIT = 16;

    public static readonly Dictionary<ModelName, string> ModelNameToId = new()
    {
        { ModelName.StateOfTheArtModel, "o1-preview" },
        { ModelName.ReasoningModel, "o1-mini" },
        { ModelName.BaseModel, "gpt-4o-2024-08-06" },
        { ModelName.FastModel, "gpt-4o-mini" },
    };

}
﻿using Jarvis.Ai.Features.StarkArsenal.ModuleAttributes;

namespace Jarvis.Ai.Features.StarkArsenal.Modules;

[JarvisTacticalModule("Returns the current time.")]
public class GetCurrentTimeJarvisModule : BaseJarvisModule
{
    protected override async Task<Dictionary<string, object>> ExecuteInternal(Dictionary<string, object> args)
    {
        return new Dictionary<string, object>
        {
            { "current_time", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") },
        };
    }
}
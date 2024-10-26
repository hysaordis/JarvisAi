using Jarvis.Ai.Common.Utils;
using Jarvis.Ai.Features.StarkArsenal.ModuleAttributes;
using Jarvis.Ai.Interfaces;

namespace Jarvis.Ai.Features.StarkArsenal;

public abstract class BaseJarvisModule : IJarvisModule
{
    public Task<Dictionary<string, object>> Execute(Dictionary<string, object> args)
    {
        return Timeit.MeasureAsync(() => ExecuteInternal(args), GetType().Name);
    }

    protected virtual async Task<Dictionary<string, object>> ExecuteInternal(Dictionary<string, object> args)
    {
        // Extract parameters dynamically before execution
        ParameterExtractionHelper.ExtractAndSetParameters(this, args);

        // Continue with the actual component execution
        return await ExecuteComponentAsync();
    }

    // New abstract method that derived classes will implement
    protected abstract Task<Dictionary<string, object>> ExecuteComponentAsync();
}
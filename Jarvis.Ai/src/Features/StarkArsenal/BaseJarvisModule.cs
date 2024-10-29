using Jarvis.Ai.Common.Utils;
using Jarvis.Ai.Features.StarkArsenal.ModuleAttributes;
using Jarvis.Ai.Interfaces;

namespace Jarvis.Ai.Features.StarkArsenal;

public abstract class BaseJarvisModule : IJarvisModule
{
    public Task<Dictionary<string, object>> Execute(Dictionary<string, object> args, CancellationToken cancellationToken = default)
    {
        return Timeit.MeasureAsync(() => ExecuteInternal(args, cancellationToken), GetType().Name);
    }

    protected virtual async Task<Dictionary<string, object>> ExecuteInternal(Dictionary<string, object> args, CancellationToken cancellationToken)
    {
        // Extract parameters dynamically before execution
        ParameterExtractionHelper.ExtractAndSetParameters(this, args);

        // Continue with the actual component execution
        return await ExecuteComponentAsync(cancellationToken);
    }

    // Modified abstract method that derived classes will implement
    protected abstract Task<Dictionary<string, object>> ExecuteComponentAsync(CancellationToken cancellationToken);
}
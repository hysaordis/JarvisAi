using Jarvis.Ai.Common.Utils;
using Jarvis.Ai.Interfaces;

namespace Jarvis.Ai.Features.StarkArsenal;

public abstract class BaseJarvisModule : IJarvisModule
{
    public Task<Dictionary<string, object>> Execute(Dictionary<string, object> args)
    {
        return Timeit.MeasureAsync(() => ExecuteInternal(args), GetType().Name);
    }

    protected abstract Task<Dictionary<string, object>> ExecuteInternal(Dictionary<string, object> args);
}
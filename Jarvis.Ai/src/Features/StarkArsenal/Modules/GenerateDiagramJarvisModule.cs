using Jarvis.Ai.Features.DiagramGeneration;
using Jarvis.Ai.Features.StarkArsenal.ModuleAttributes;

namespace Jarvis.Ai.Features.StarkArsenal.Modules
{
    [JarvisTacticalModule("Generates mermaid diagrams based on the user's prompt.")]
    public class GenerateDiagramJarvisModule : BaseJarvisModule
    {
        [TacticalComponent("The user's prompt describing the diagram to generate.", "string", true)]
        public string Prompt { get; set; }

        [TacticalComponent("The total number of diagram versions to generate. Defaults to 1 if not specified.", "integer")]
        public int VersionCount { get; set; } = 1;

        private readonly DiagramGenerationTool _diagramGenerationTool;

        public GenerateDiagramJarvisModule(DiagramGenerationTool diagramGenerationTool)
        {
            _diagramGenerationTool = diagramGenerationTool;
        }

        protected override async Task<Dictionary<string, object>> ExecuteComponentAsync(CancellationToken cancellationToken)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                var result = await _diagramGenerationTool.GenerateDiagram(Prompt, VersionCount);
                return result;
            }
            catch (OperationCanceledException)
            {
                return new Dictionary<string, object>
                {
                    { "status", "cancelled" },
                    { "message", "Operation was cancelled" }
                };
            }
            catch (Exception e)
            {
                return new Dictionary<string, object>
                {
                    { "status", "error" },
                    { "message", $"Failed to generate diagram: {e.Message}" },
                };
            }
        }
    }
}

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
        public int VersionCount { get; set; }
        
        private readonly DiagramGenerationTool _diagramGenerationTool;

        public GenerateDiagramJarvisModule(DiagramGenerationTool diagramGenerationTool)
        {
            _diagramGenerationTool = diagramGenerationTool;
        }

        protected override async Task<Dictionary<string, object>> ExecuteInternal(Dictionary<string, object> args)
        {
            try
            {
                string prompt = args["Prompt"].ToString();
                int versionCount = args.ContainsKey("version_count") ? Convert.ToInt32(args["version_count"]) : 1;
                var result = await _diagramGenerationTool.GenerateDiagram(prompt, versionCount);
                return result;
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
using System.Text;
using Jarvis.Ai.Features.StarkArsenal.ModuleAttributes;
using Jarvis.Ai.Interfaces;
using SixLabors.ImageSharp;

namespace Jarvis.Ai.Features.StarkArsenal.Modules
{
    public class MermaidResponse
    {
        public string BaseName { get; set; }
        public List<string> MermaidDiagrams { get; set; }
    }

    [JarvisTacticalModule("Generates mermaid diagrams based on the user's prompt.")]
    public class GenerateDiagramJarvisModule : BaseJarvisModule
    {
        [TacticalComponent("The user's prompt describing the diagram to generate.", "string", true)]
        public string Prompt { get; set; }

        [TacticalComponent("The total number of diagram versions to generate. Defaults to 1 if not specified.", "integer")]
        public int VersionCount { get; set; } = 1;

        private readonly HttpClient _httpClient = new();
        private readonly IMemoryManager _memoryManager;
        private readonly ILlmClient _llmClient;
        private readonly string _scratchPadDir;

        public GenerateDiagramJarvisModule(
            IMemoryManager memoryManager,
            IJarvisConfigManager configManager,
            ILlmClient llmClient)
        {
            var scratchPadDir = configManager.GetValue("SCRATCH_PAD_DIR");
            if (string.IsNullOrEmpty(scratchPadDir))
            {
                throw new Exception("SCRATCH_PAD_DIR environment variable not set.");
            }
            _memoryManager = memoryManager;
            _scratchPadDir = scratchPadDir;
            _llmClient = llmClient;
        }

        private string BuildFilePath(string name)
        {
            if (!Directory.Exists(_scratchPadDir))
            {
                Directory.CreateDirectory(_scratchPadDir);
            }
            return Path.Combine(_scratchPadDir, name);
        }

        private async Task<Image> BuildImage(string graph, string filename)
        {
            var graphbytes = Encoding.UTF8.GetBytes(graph);
            var base64String = Convert.ToBase64String(graphbytes);
            var url = $"https://mermaid.ink/img/{base64String}";

            var response = await _httpClient.GetAsync(url);
            try
            {
                using (var stream = await response.Content.ReadAsStreamAsync())
                {
                    var img = await Image.LoadAsync(stream);
                    return img;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: Unable to generate image for '{filename}': {ex.Message}");
                return null;
            }
        }

        private async Task<Image> Mm(string graph, string filename)
        {
            var img = await BuildImage(graph, filename);
            if (img != null)
            {
                string outputPath = BuildFilePath(filename);
                await img.SaveAsPngAsync(outputPath);
                return img;
            }
            return null;
        }

        protected override async Task<Dictionary<string, object>> ExecuteComponentAsync(CancellationToken cancellationToken)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                string memoryContent = _memoryManager.GetXmlForPrompt(new List<string> { "*" });
                string mermaidPrompt = $@"
<purpose>
    Generate {VersionCount} mermaid diagram(s) based on the user's prompt and the current memory content.
</purpose>

<instructions>
    <instruction>For each version, create a unique mermaid diagram code that represents the user's prompt.</instruction>
    <instruction>Generate a suitable 'base_name' for the filenames based on the user's prompt. Use lowercase letters, numbers, and underscores only.</instruction>
    <instruction>Only provide the 'base_name' and the list of mermaid diagram codes in a dictionary format, without any additional text or formatting.</instruction>
    <instruction>Consider the current memory content when generating the diagrams, if relevant.</instruction>
</instructions>

<user_prompt>
    {Prompt}
</user_prompt>

{memoryContent}
";

                var response = await _llmClient.StructuredOutputPrompt<MermaidResponse>(mermaidPrompt);
                string baseName = response.BaseName;

                var diagramsInfo = new List<Dictionary<string, object>>();
                int successfulCount = 0;
                int failedCount = 0;

                for (int i = 0; i < response.MermaidDiagrams.Count; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    
                    string mermaidCode = response.MermaidDiagrams[i];
                    string imageFilename = $"diagram_{baseName}_{i + 1}.png";
                    string textFilename = $"diagram_text_{baseName}_{i + 1}.md";

                    var img = await Mm(mermaidCode, imageFilename);

                    if (img != null)
                    {
                        string textFilePath = BuildFilePath(textFilename);
                        File.WriteAllText(textFilePath, mermaidCode);

                        successfulCount++;
                        diagramsInfo.Add(new Dictionary<string, object>
                        {
                            { "version", i + 1 },
                            { "image_file", BuildFilePath(imageFilename) },
                            { "text_file", textFilePath },
                            { "mermaid_code", mermaidCode }
                        });
                    }
                    else
                    {
                        failedCount++;
                    }
                }

                string message = successfulCount > 0
                    ? $"Generated {successfulCount} diagram(s){(failedCount > 0 ? $"; {failedCount} diagram(s) failed to generate" : "")}"
                    : "No diagrams were generated successfully.";

                string status = successfulCount > 0 ? "success" : "failure";

                return new Dictionary<string, object>
                {
                    { "status", status },
                    { "message", message },
                    { "diagrams_info", diagramsInfo }
                };
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

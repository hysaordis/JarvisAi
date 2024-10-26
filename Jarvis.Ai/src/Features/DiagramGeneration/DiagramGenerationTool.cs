using System.Text;
using Jarvis.Ai.Interfaces;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SixLabors.ImageSharp;

namespace Jarvis.Ai.Features.DiagramGeneration;

public class MermaidResponse
{
    public string BaseName { get; set; }
    public List<string> MermaidDiagrams { get; set; }
}

public class DiagramGenerationTool
{
    private readonly HttpClient _httpClient = new();
    private readonly IMemoryManager _memoryManager;
    private readonly string _scratchPadDir;
    private readonly string _apiKey;

    public DiagramGenerationTool(IMemoryManager memoryManager,IJarvisConfigManager configManager)
    {
        var key = configManager.GetValue("OPENAI_API_KEY");
        var scratchPadDir = configManager.GetValue("SCRATCH_PAD_DIR");
        if (string.IsNullOrEmpty(key))
        {
            throw new Exception("OPENAI_API_KEY environment variable not set.");
        }
        if (string.IsNullOrEmpty(scratchPadDir))
        {
            throw new Exception("SCRATCH_PAD_DIR environment variable not set.");
        }
        _memoryManager = memoryManager;
        _scratchPadDir = scratchPadDir;
        _apiKey = key;
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
        else
        {
            return null;
        }
    }

    public async Task<Dictionary<string, object>> GenerateDiagram(string prompt, int versionCount = 1)
    {
        string memoryContent = _memoryManager.GetXmlForPrompt(new List<string> { "*" });

        string mermaidPrompt = $@"
<purpose>
    Generate {versionCount} mermaid diagram(s) based on the user's prompt and the current memory content.
</purpose>

<instructions>
    <instruction>For each version, create a unique mermaid diagram code that represents the user's prompt.</instruction>
    <instruction>Generate a suitable 'base_name' for the filenames based on the user's prompt. Use lowercase letters, numbers, and underscores only.</instruction>
    <instruction>Only provide the 'base_name' and the list of mermaid diagram codes in a dictionary format, without any additional text or formatting.</instruction>
    <instruction>Consider the current memory content when generating the diagrams, if relevant.</instruction>
</instructions>

<user_prompt>
    {prompt}
</user_prompt>

{memoryContent}
";

        var response = await StructuredOutputPrompt(mermaidPrompt);
        string baseName = response.BaseName;

        var diagramsInfo = new List<Dictionary<string, object>>();
        int successfulCount = 0;
        int failedCount = 0;

        for (int i = 0; i < response.MermaidDiagrams.Count; i++)
        {
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
                continue;
            }
        }

        string message;
        string status;
        if (successfulCount > 0)
        {
            message = $"Generated {successfulCount} diagram(s)";
            if (failedCount > 0)
            {
                message += $"; {failedCount} diagram(s) failed to generate";
            }

            status = "success";
        }
        else
        {
            message = "No diagrams were generated successfully.";
            status = "failure";
        }

        return new Dictionary<string, object>
        {
            { "status", status },
            { "message", message },
            { "diagrams_info", diagramsInfo }
        };
    }

    private async Task<MermaidResponse> StructuredOutputPrompt(string prompt, string llmModel = "gpt-4")
    {
        if (string.IsNullOrEmpty(_apiKey))
        {
            throw new Exception("OPENAI_API_KEY environment variable not set.");
        }

        using (var client = new HttpClient())
        {
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");

            var requestBody = new
            {
                model = llmModel,
                messages = new[]
                {
                    new { role = "user", content = prompt }
                },
                temperature = 0.7,
            };

            var jsonContent = JsonConvert.SerializeObject(requestBody);
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            var response = await client.PostAsync("https://api.openai.com/v1/chat/completions", content);

            if (response.IsSuccessStatusCode)
            {
                var responseString = await response.Content.ReadAsStringAsync();
                var responseJson = JObject.Parse(responseString);

                var messageContent = responseJson["choices"][0]["message"]["content"].ToString();

                try
                {
                    // Parse the assistant's response as JSON
                    var mermaidResponse = JsonConvert.DeserializeObject<MermaidResponse>(messageContent);
                    return mermaidResponse;
                }
                catch (Exception ex)
                {
                    throw new Exception("Failed to parse the assistant's response into MermaidResponse: " + ex.Message);
                }
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                throw new Exception($"OpenAI API request failed: {errorContent}");
            }
        }
    }
}
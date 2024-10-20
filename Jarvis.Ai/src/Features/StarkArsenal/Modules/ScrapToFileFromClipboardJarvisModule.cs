﻿using Jarvis.Ai.Common.Settings;
using Jarvis.Ai.Features.StarkArsenal.ModuleAttributes;
using Jarvis.Ai.Features.WebDataExtraction;
using Jarvis.Ai.Models;
using Jarvis.Ai.src.Interfaces;
using TextCopy;

namespace Jarvis.Ai.Features.StarkArsenal.Modules;

[JarvisTacticalModule("Gets a URL from the clipboard, scrapes its content, and saves it to a file in the scratch_pad_dir.")]
public class ScrapToFileFromClipboardJarvisModule : BaseJarvisModule
{
    private readonly IJarvisConfigManager _jarvisConfigManager;
    private readonly LlmClient _llmClient;
    private readonly FireCrawlApp _fireCrawlApp;

    public ScrapToFileFromClipboardJarvisModule(IJarvisConfigManager jarvisConfigManager, LlmClient llmClient,
        FireCrawlApp fireCrawlApp)
    {
        _jarvisConfigManager = jarvisConfigManager;
        _llmClient = llmClient;
        _fireCrawlApp = fireCrawlApp;
    }

    protected override async Task<Dictionary<string, object>> ExecuteInternal(Dictionary<string, object> args)
    {
        string scratchPadDir = _jarvisConfigManager.GetValue("SCRATCH_PAD_DIR") ?? "./scratchpad";
        Directory.CreateDirectory(scratchPadDir);

        try
        {
            // Get URL from clipboard
            string? url = await ClipboardService.GetTextAsync();
            url = url!.Trim();

            // Validate URL
            if (!Uri.IsWellFormedUriString(url, UriKind.Absolute))
            {
                return new Dictionary<string, object>
                {
                    { "status", "error" },
                    { "message", "Clipboard content is not a valid URL" },
                };
            }

            // Generate file name using LLMClient
            string fileNamePrompt = $@"
<purpose>
    Generate a suitable file name for the content of this URL: {url}
</purpose>

<instructions>
    <instruction>Create a short, descriptive file name based on the URL.</instruction>
    <instruction>Use lowercase letters, numbers, and underscores only.</instruction>
    <instruction>Include the .md extension at the end.</instruction>
</instructions>
";

            CreateFileResponse fileNameResponse =
                await _llmClient.StructuredOutputPrompt<CreateFileResponse>(fileNamePrompt,
                    Constants.ModelNameToId[ModelName.FastModel]);
            string fileName = fileNameResponse.FileName;

            // Scrape URL content using FirecrawlApp
            var scrapeResult = await _fireCrawlApp.ScrapeUrl(url);

            string content = scrapeResult.ContainsKey("content") ? scrapeResult["content"].ToString() : "";

            // Save to file
            string filePath = Path.Combine(scratchPadDir, fileName);
            await File.WriteAllTextAsync(filePath, content);

            return new Dictionary<string, object>
            {
                { "status", "success" },
                { "message", $"Content scraped and saved to {filePath}" },
                { "file_name", fileName },
            };
        }
        catch (Exception e)
        {
            return new Dictionary<string, object>
            {
                { "status", "error" },
                { "message", $"Failed to scrape URL and save to file: {e.Message}" },
            };
        }
    }
}
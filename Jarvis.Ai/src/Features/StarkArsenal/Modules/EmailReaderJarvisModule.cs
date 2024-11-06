using System.Text;
using System.Text.RegularExpressions;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Gmail.v1;
using Google.Apis.Gmail.v1.Data;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using Jarvis.Ai.Common.Settings;
using Jarvis.Ai.Features.StarkArsenal.ModuleAttributes;
using Jarvis.Ai.Interfaces;
using Jarvis.Ai.LLM;
using Jarvis.Ai.Models;

namespace Jarvis.Ai.Features.StarkArsenal.Modules;

/// <summary>
/// Shared utilities for email processing
/// </summary>
public static class EmailUtils
{
    public static class TextCleaner
    {
        public static string CleanText(string input)
        {
            if (string.IsNullOrEmpty(input))
                return string.Empty;

            // Remove CSS and HTML/XML content
            input = Regex.Replace(input, @"\{[^\}]*\}", string.Empty);
            input = Regex.Replace(input, @"<[^>]+>", string.Empty);
            
            // Replace common HTML entities
            var entities = new Dictionary<string, string>
            {
                { "&nbsp;", " " }, { "&amp;", "&" }, { "&lt;", "<" }, 
                { "&gt;", ">" }, { "&#39;", "'" }, { "&apos;", "'" },
                { "&quot;", "\"" }, { "&ndash;", "-" }, { "&mdash;", "-" },
                { "&euro;", "€" }, { "&pound;", "£" }, { "&reg;", "®" },
                { "&copy;", "©" }, { "&trade;", "™" }
            };

            foreach (var entity in entities)
            {
                input = input.Replace(entity.Key, entity.Value);
            }

            // Remove email-specific formatting and clean up whitespace
            input = Regex.Replace(input, @"͏", string.Empty);
            input = Regex.Replace(input, @"\r\n|\r|\n", "\n");
            input = Regex.Replace(input, @"\n{3,}", "\n\n");
            input = Regex.Replace(input, @"\s+", " ");
            
            // Keep only valid characters and essential punctuation
            input = Regex.Replace(input, @"[^\w\s\.,!?@\-:;()\[\]""'/\n]", " ");
            
            return input.Trim();
        }

        public static string DecodeBase64Email(string base64)
        {
            if (string.IsNullOrEmpty(base64))
                return string.Empty;

            try
            {
                base64 = base64.Replace('-', '+').Replace('_', '/');
                var data = Convert.FromBase64String(base64);
                return Encoding.UTF8.GetString(data);
            }
            catch
            {
                return string.Empty;
            }
        }

        public static string ExtractEmailContent(MessagePart messagePart)
        {
            var content = new StringBuilder();

            if (messagePart?.Body?.Data != null)
            {
                string decodedContent = DecodeBase64Email(messagePart.Body.Data);
                content.AppendLine(CleanText(decodedContent));
            }

            if (messagePart?.Parts != null)
            {
                foreach (var part in messagePart.Parts)
                {
                    if (part.MimeType == "text/plain" || part.MimeType == "text/html")
                    {
                        content.AppendLine(ExtractEmailContent(part));
                    }
                }
            }

            return content.ToString().Trim();
        }
    }

    public class EmailDetails
    {
        public string Id { get; set; }
        public string Subject { get; set; }
        public string From { get; set; }
        public string To { get; set; }
        public DateTime ReceivedTime { get; set; }
        public string Content { get; set; }
        public string Snippet { get; set; }
    }

    public static async Task<GmailService> InitializeGmailServiceAsync(
        string credentialsPath, 
        string tokenPath, 
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(credentialsPath))
            throw new InvalidOperationException("Gmail configuration is missing. Please set GMAIL_CREDENTIALS_PATH in config.");

        using var stream = new FileStream(credentialsPath, FileMode.Open, FileAccess.Read);
        var credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
            GoogleClientSecrets.FromStream(stream).Secrets,
            new[] { GmailService.Scope.GmailReadonly },
            "user",
            cancellationToken,
            new FileDataStore(tokenPath, true));

        return new GmailService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = "Jarvis Email Reader"
        });
    }

    public static async Task<EmailDetails> GetEmailDetailsAsync(
        GmailService service, 
        string messageId, 
        CancellationToken cancellationToken)
    {
        var email = await service.Users.Messages.Get("me", messageId).ExecuteAsync(cancellationToken);
        var headers = email.Payload.Headers;

        return new EmailDetails
        {
            Id = messageId,
            Subject = TextCleaner.CleanText(headers.FirstOrDefault(h => h.Name == "Subject")?.Value ?? "(no subject)"),
            From = TextCleaner.CleanText(headers.FirstOrDefault(h => h.Name == "From")?.Value ?? "(unknown sender)"),
            To = TextCleaner.CleanText(headers.FirstOrDefault(h => h.Name == "To")?.Value ?? "(no recipients)"),
            ReceivedTime = DateTime.TryParse(headers.FirstOrDefault(h => h.Name == "Date")?.Value, out var dt) ? dt : DateTime.Now,
            Content = TextCleaner.ExtractEmailContent(email.Payload),
            Snippet = TextCleaner.CleanText(email.Snippet)
        };
    }
}

[JarvisTacticalModule(
    @"Gmail inbox manager: read unread emails, summarize content, check inbox, show notifications.
    Triggers: check emails/mail, show new messages, read unread, get updates, show inbox, summarize emails")]
public class EmailSummaryJarvisModule : BaseJarvisModule
{
    private readonly IJarvisConfigManager _configManager;
    private readonly ILlmClient _llmClient;

    public EmailSummaryJarvisModule(IJarvisConfigManager configManager, ILlmClient llmClient)
    {
        _configManager = configManager;
        _llmClient = llmClient;
    }

    protected override async Task<Dictionary<string, object>> ExecuteComponentAsync(CancellationToken cancellationToken)
    {
        try
        {
            string credentialsPath = _configManager.GetValue("GMAIL_CREDENTIALS_PATH");
            string tokenPath = _configManager.GetValue("TOKEN_PATH") ?? "config/token";

            var service = await EmailUtils.InitializeGmailServiceAsync(credentialsPath, tokenPath, cancellationToken);

            // Search for unread emails
            var listRequest = service.Users.Messages.List("me");
            listRequest.Q = "is:unread";
            listRequest.MaxResults = 50;

            var messages = await listRequest.ExecuteAsync(cancellationToken);

            if (messages.Messages == null || !messages.Messages.Any())
            {
                return new Dictionary<string, object>
                {
                    { "status", "success" },
                    { "message", "No unread emails found." }
                };
            }

            // Get email details
            var emailSummaries = new List<EmailUtils.EmailDetails>();
            foreach (var message in messages.Messages)
            {
                var emailDetails = await EmailUtils.GetEmailDetailsAsync(service, message.Id, cancellationToken);
                emailSummaries.Add(emailDetails);
            }

            // Analyze emails
            string analysisPrompt = $@"
<purpose>Analyze the following emails and identify important ones.</purpose>

<emails>
{string.Join("\n\n", emailSummaries.Select(e => $"Email from: {e.From}\nSubject: {e.Subject}\nContent:\n{e.Content}"))}
</emails>

<instructions>
Provide a summary:
Total new emails: [number]

Important emails:
- From [sender] re: [subject]:
  [brief 2-sentence summary]

Other emails:
- List of remaining (sender and subject only)
</instructions>";

            string analysis = await _llmClient.ChatPrompt(analysisPrompt, Constants.ModelNameToId[ModelName.FastModel]);

            var response = new StringBuilder();
            response.AppendLine(analysis);
            response.AppendLine("\nEmail IDs for reference:");
            foreach (var email in emailSummaries)
            {
                response.AppendLine($"- From: {email.From}");
                response.AppendLine($"  Subject: {email.Subject}");
                response.AppendLine($"  ID: {email.Id}");
                response.AppendLine();
            }

            return new Dictionary<string, object>
            {
                { "status", "success" },
                { "message", response.ToString() },
                { "unread_count", emailSummaries.Count },
                { "email_ids", emailSummaries.Select(e => e.Id).ToList() }
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new Dictionary<string, object>
            {
                { "status", "error" },
                { "message", $"Failed to process emails: {ex.Message}" }
            };
        }
    }
}

[JarvisTacticalModule(
    @"Email reader and formatter: read specific email, show details, get content.
    Triggers: read email, show message, get content, view message, open email")]
public class EmailReaderJarvisModule : BaseJarvisModule
{
    [TacticalComponent("Gmail message ID to read. Example: 'read email ID123'", "string", true)]
    public string EmailId { get; set; }

    [TacticalComponent("Display mode: 'full', 'summary', or 'important'", "string")]
    public string DisplayMode { get; set; } = "summary";

    private readonly IJarvisConfigManager _configManager;
    private readonly ILlmClient _llmClient;

    private enum DisplayModes { Summary, Full, Important }

    public EmailReaderJarvisModule(IJarvisConfigManager configManager, ILlmClient llmClient)
    {
        _configManager = configManager;
        _llmClient = llmClient;
    }

    protected override async Task<Dictionary<string, object>> ExecuteComponentAsync(CancellationToken cancellationToken)
    {
        try
        {
            var displayMode = (DisplayMode?.ToLower() ?? "summary") switch
            {
                "full" or "complete" or "entire" => DisplayModes.Full,
                "important" or "key" or "main" => DisplayModes.Important,
                _ => DisplayModes.Summary
            };

            string credentialsPath = _configManager.GetValue("GMAIL_CREDENTIALS_PATH");
            string tokenPath = _configManager.GetValue("TOKEN_PATH") ?? "config/token";

            var service = await EmailUtils.InitializeGmailServiceAsync(credentialsPath, tokenPath, cancellationToken);
            var emailDetails = await EmailUtils.GetEmailDetailsAsync(service, EmailId, cancellationToken);
            
            var response = new StringBuilder();
            response.AppendLine("📧 Email Details:");
            response.AppendLine($"From: {emailDetails.From}");
            response.AppendLine($"To: {emailDetails.To}");
            response.AppendLine($"Subject: {emailDetails.Subject}");
            response.AppendLine($"Received: {emailDetails.ReceivedTime:yyyy-MM-dd HH:mm:ss}");
            response.AppendLine($"ID: {emailDetails.Id}");
            response.AppendLine();

            switch (displayMode)
            {
                case DisplayModes.Full:
                    response.AppendLine("📝 Full Content:");
                    response.AppendLine(emailDetails.Content);
                    break;

                case DisplayModes.Important:
                    string importantPrompt = 
                        "<purpose>Extract key points and action items from this email.</purpose>\n\n" +
                        "<email-content>\n" + emailDetails.Content + "\n</email-content>\n\n" +
                        "<instructions>\n" +
                        "- List main points and key information\n" +
                        "- Highlight any action items or deadlines\n" +
                        "- Include crucial details\n" +
                        "</instructions>";

                    string important = await _llmClient.ChatPrompt(importantPrompt, Constants.ModelNameToId[ModelName.BaseModel]);
                    response.AppendLine("🔑 Key Points:");
                    response.AppendLine(important);
                    break;

                default:
                    string summaryPrompt = 
                        "<purpose>Provide a concise summary of this email.</purpose>\n\n" +
                        "<email-content>\n" + emailDetails.Content + "\n</email-content>\n\n" +
                        "<instructions>\n" +
                        "- Summarize the main message in 2-3 sentences\n" +
                        "- Include any immediate action items\n" +
                        "- Keep it brief but informative\n" +
                        "</instructions>";

                    string summary = await _llmClient.ChatPrompt(summaryPrompt, Constants.ModelNameToId[ModelName.FastModel]);
                    response.AppendLine("📋 Summary:");
                    response.AppendLine(summary);
                    break;
            }

            return new Dictionary<string, object>
            {
                { "status", "success" },
                { "message", response.ToString() },
                { "email_id", EmailId },
                { "display_mode", displayMode.ToString() }
            };
        }
        catch (Google.GoogleApiException ex) when (ex.HttpStatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return new Dictionary<string, object>
            {
                { "status", "error" },
                { "message", $"Email not found. Please verify the Email ID: {EmailId}" }
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new Dictionary<string, object>
            {
                { "status", "error" },
                { "message", $"Failed to read email: {ex.Message}" }
            };
        }
    }
}
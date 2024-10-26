using System.Text;
using Jarvis.Ai.Interfaces;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Jarvis.Ai.Features.WebDataExtraction;

public class FireCrawlApp
{
    private readonly string _apiKey;
    private string apiUrl;
    private static readonly HttpClient httpClient = new HttpClient();

    public FireCrawlApp(IJarvisConfigManager configManager)
    {
        _apiKey = configManager.GetValue("FIRECRAWL_API_KEY");
        if (string.IsNullOrEmpty(_apiKey))
        {
            Console.WriteLine("No API key provided");
            throw new ArgumentException("No API key provided");
        }

        Console.WriteLine($"Initialized FirecrawlApp with API key: {_apiKey}");

        apiUrl = apiUrl ?? "https://api.firecrawl.dev";
        if (apiUrl != "https://api.firecrawl.dev")
        {
            Console.WriteLine($"Initialized FirecrawlApp with API URL: {apiUrl}");
        }
    }


    public async Task<Dictionary<string, object>> ScrapeUrl(string url, Dictionary<string, object> parameters = null)
    {
        var headers = PrepareHeaders();

        var scrapeParams = new Dictionary<string, object>
        {
            { "url", url }
        };

        if (parameters != null)
        {
            if (parameters.ContainsKey("extractorOptions"))
            {
                var extractorOptions = parameters["extractorOptions"] as Dictionary<string, object> ??
                                       new Dictionary<string, object>();

                if (extractorOptions.ContainsKey("extractionSchema"))
                {
                    if (!extractorOptions.ContainsKey("mode"))
                    {
                        extractorOptions["mode"] = "llm-extraction";
                    }

                    scrapeParams["extractorOptions"] = extractorOptions;
                }
                else
                {
                    scrapeParams["extractorOptions"] = extractorOptions;
                }
            }

            foreach (var kvp in parameters)
            {
                if (kvp.Key != "extractorOptions")
                {
                    scrapeParams[kvp.Key] = kvp.Value;
                }
            }
        }

        var jsonContent = JsonConvert.SerializeObject(scrapeParams);
        var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

        var request = new HttpRequestMessage(HttpMethod.Post, $"{apiUrl}/v0/scrape");
        request.Content = content;

        foreach (var header in headers)
        {
            request.Headers.Add(header.Key, header.Value);
        }

        var response = await httpClient.SendAsync(request);

        if (response.StatusCode == System.Net.HttpStatusCode.OK)
        {
            var responseString = await response.Content.ReadAsStringAsync();
            var responseData = JsonConvert.DeserializeObject<Dictionary<string, object>>(responseString);

            if (responseData != null && responseData.ContainsKey("success") && (bool)responseData["success"] &&
                responseData.ContainsKey("data"))
            {
                var data = responseData["data"] as JObject;
                var dataDict = data.ToObject<Dictionary<string, object>>();
                return dataDict;
            }
            else
            {
                throw new Exception($"Failed to scrape URL. Error: {responseData["error"]}");
            }
        }
        else
        {
            await HandleError(response, "scrape URL");
            return null;
        }
    }

    public async Task<object> Search(string query, Dictionary<string, object> parameters = null)
    {
        var headers = PrepareHeaders();
        var jsonData = new Dictionary<string, object>
        {
            { "query", query }
        };
        if (parameters != null)
        {
            foreach (var kvp in parameters)
            {
                jsonData[kvp.Key] = kvp.Value;
            }
        }

        var jsonContent = JsonConvert.SerializeObject(jsonData);
        var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

        var request = new HttpRequestMessage(HttpMethod.Post, $"{apiUrl}/v0/search");
        request.Content = content;

        foreach (var header in headers)
        {
            request.Headers.Add(header.Key, header.Value);
        }

        var response = await httpClient.SendAsync(request);

        if (response.StatusCode == System.Net.HttpStatusCode.OK)
        {
            var responseString = await response.Content.ReadAsStringAsync();
            var responseData = JsonConvert.DeserializeObject<Dictionary<string, object>>(responseString);

            if (responseData != null && responseData.ContainsKey("success") && (bool)responseData["success"] &&
                responseData.ContainsKey("data"))
            {
                return responseData["data"];
            }
            else
            {
                throw new Exception($"Failed to search. Error: {responseData["error"]}");
            }
        }
        else
        {
            await HandleError(response, "search");
            return null;
        }
    }

    public async Task<object> CrawlUrl(string url,
        Dictionary<string, object> parameters = null,
        bool waitUntilDone = true,
        int pollInterval = 2,
        string idempotencyKey = null)
    {
        var headers = PrepareHeaders(idempotencyKey);

        var jsonData = new Dictionary<string, object>
        {
            { "url", url }
        };

        if (parameters != null)
        {
            foreach (var kvp in parameters)
            {
                jsonData[kvp.Key] = kvp.Value;
            }
        }

        var response = await PostRequestAsync($"{apiUrl}/v0/crawl", jsonData, headers);

        if (response.StatusCode == System.Net.HttpStatusCode.OK)
        {
            var responseString = await response.Content.ReadAsStringAsync();
            var responseData = JsonConvert.DeserializeObject<Dictionary<string, object>>(responseString);

            if (responseData != null && responseData.ContainsKey("jobId"))
            {
                var jobId = responseData["jobId"].ToString();
                if (waitUntilDone)
                {
                    return await MonitorJobStatus(jobId, headers, pollInterval);
                }
                else
                {
                    return new Dictionary<string, object> { { "jobId", jobId } };
                }
            }
            else
            {
                throw new Exception("Failed to get jobId from response");
            }
        }
        else
        {
            await HandleError(response, "start crawl job");
            return null;
        }
    }

    public async Task<object> CheckCrawlStatus(string jobId)
    {
        var headers = PrepareHeaders();
        var response = await GetRequestAsync($"{apiUrl}/v0/crawl/status/{jobId}", headers);
        if (response.StatusCode == System.Net.HttpStatusCode.OK)
        {
            var responseString = await response.Content.ReadAsStringAsync();
            var responseData = JsonConvert.DeserializeObject<Dictionary<string, object>>(responseString);
            return responseData;
        }
        else
        {
            await HandleError(response, "check crawl status");
            return null;
        }
    }

    private Dictionary<string, string> PrepareHeaders(string idempotencyKey = null)
    {
        var headers = new Dictionary<string, string>
        {
            { "Authorization", $"Bearer {_apiKey}" }
        };

        if (!string.IsNullOrEmpty(idempotencyKey))
        {
            headers["x-idempotency-key"] = idempotencyKey;
        }

        return headers;
    }

    private async Task<HttpResponseMessage> PostRequestAsync(string url,
        object data,
        Dictionary<string, string> headers,
        int retries = 3,
        double backoffFactor = 0.5)
    {
        int attempt = 0;
        HttpResponseMessage response = null;
        while (attempt < retries)
        {
            var jsonContent = JsonConvert.SerializeObject(data);
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Content = content;

            foreach (var header in headers)
            {
                request.Headers.Add(header.Key, header.Value);
            }

            response = await httpClient.SendAsync(request);

            if ((int)response.StatusCode == 502)
            {
                await Task.Delay(TimeSpan.FromSeconds(backoffFactor * Math.Pow(2, attempt)));
                attempt++;
            }
            else
            {
                return response;
            }
        }

        return response;
    }

    private async Task<HttpResponseMessage> GetRequestAsync(string url,
        Dictionary<string, string> headers,
        int retries = 3,
        double backoffFactor = 0.5)
    {
        int attempt = 0;
        HttpResponseMessage response = null;
        while (attempt < retries)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, url);

            foreach (var header in headers)
            {
                request.Headers.Add(header.Key, header.Value);
            }

            response = await httpClient.SendAsync(request);

            if ((int)response.StatusCode == 502)
            {
                await Task.Delay(TimeSpan.FromSeconds(backoffFactor * Math.Pow(2, attempt)));
                attempt++;
            }
            else
            {
                return response;
            }
        }

        return response;
    }

    private async Task<object> MonitorJobStatus(string jobId, Dictionary<string, string> headers, int pollInterval)
    {
        while (true)
        {
            var statusResponse = await GetRequestAsync($"{apiUrl}/v0/crawl/status/{jobId}", headers);
            if (statusResponse.StatusCode == System.Net.HttpStatusCode.OK)
            {
                var responseString = await statusResponse.Content.ReadAsStringAsync();
                var statusData = JsonConvert.DeserializeObject<Dictionary<string, object>>(responseString);

                if (statusData != null && statusData.ContainsKey("status"))
                {
                    var status = statusData["status"].ToString();
                    if (status == "completed")
                    {
                        if (statusData.ContainsKey("data"))
                        {
                            return statusData["data"];
                        }
                        else
                        {
                            throw new Exception("Crawl job completed but no data was returned");
                        }
                    }
                    else if (new List<string> { "active", "paused", "pending", "queued", "waiting" }.Contains(status))
                    {
                        pollInterval = Math.Max(pollInterval, 2);
                        await Task.Delay(TimeSpan.FromSeconds(pollInterval));
                    }
                    else
                    {
                        throw new Exception($"Crawl job failed or was stopped. Status: {status}");
                    }
                }
                else
                {
                    throw new Exception("Failed to get status from response");
                }
            }
            else
            {
                await HandleError(statusResponse, "check crawl status");
                return null;
            }
        }
    }

    private async Task HandleError(HttpResponseMessage response, string action)
    {
        var responseString = await response.Content.ReadAsStringAsync();
        string errorMessage = "No additional error details provided.";

        try
        {
            var responseData = JsonConvert.DeserializeObject<Dictionary<string, object>>(responseString);
            if (responseData != null && responseData.ContainsKey("error"))
            {
                errorMessage = responseData["error"].ToString();
            }
        }
        catch
        {
            // Ignore JSON parsing errors
        }

        string message;
        switch ((int)response.StatusCode)
        {
            case 402:
                message = $"Payment Required: Failed to {action}. {errorMessage}";
                break;
            case 408:
                message = $"Request Timeout: Failed to {action} as the request timed out. {errorMessage}";
                break;
            case 409:
                message = $"Conflict: Failed to {action} due to a conflict. {errorMessage}";
                break;
            case 500:
                message = $"Internal Server Error: Failed to {action}. {errorMessage}";
                break;
            default:
                message = $"Unexpected error during {action}: Status code {(int)response.StatusCode}. {errorMessage}";
                break;
        }

        throw new HttpRequestException(message);
    }
}
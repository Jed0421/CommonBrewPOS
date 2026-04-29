using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace CommonBrewPOS.Services;

public class AiChatbotService
{
    private readonly HttpClient _http;
    private readonly string _apiKey;

    public AiChatbotService(HttpClient http, IConfiguration config)
    {
        _http = http;
        _apiKey = config["Groq:ApiKey"] ?? "";
    }
    
    public async Task<string> AskAsync(string userQuestion)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(_apiKey))
                return "Groq API key is missing in appsettings.json.";

            var payload = new
            {
                model = "llama-3.3-70b-versatile",
                messages = new object[]
                {
                    new
                    {
                        role = "system",
                        content = """
                        You are CommonBot, the official assistant of CommonBrew Taytay POS.

                        Always introduce yourself as CommonBot.
                        Never call yourself "AI assistant".
                        Be concise, friendly, and helpful.
                        Use Philippine Peso (₱) for all currency.
                        Answer based on the database information provided by the system.
                        Call the logged-in user by their name when appropriate.
                        """
                    },
                    new
                    {
                        role = "user",
                        content = userQuestion
                    }
                },
                temperature = 0.2,
                max_completion_tokens = 500
            };

            var jsonPayload = JsonSerializer.Serialize(payload);

            using var req = new HttpRequestMessage(
                HttpMethod.Post,
                "https://api.groq.com/openai/v1/chat/completions");

            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
            req.Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            var response = await _http.SendAsync(req);
            var body = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                return $"Groq error {(int)response.StatusCode}: {body}";
            }

            using var doc = JsonDocument.Parse(body);

            return doc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString() ?? "No response.";
        }
        catch (Exception ex)
        {
            return $"Chatbot error: {ex.Message}";
        }
    }
}
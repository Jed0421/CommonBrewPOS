using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace CommonBrewPOS.Services;

public class AiChatbotService
{
    private readonly HttpClient _http;
    private readonly string _apiKey;
    private readonly ReportService _report;
    private readonly InventoryService _inventory;

    public AiChatbotService(
        HttpClient http,
        IConfiguration config,
        ReportService report,
        InventoryService inventory)
    {
        _http = http;
        _apiKey = config["Groq:ApiKey"] ?? "";
        _report = report;
        _inventory = inventory;
    }

    public async Task<string> AskAsync(string userQuestion)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(_apiKey))
                return "Groq API key is missing in appsettings.json.";

            var analytics = await _report.GetDashboardAnalyticsAsync("Today");
            var lowStock = await _inventory.GetLowStockAsync();
            //Hi
            var systemPrompt = $"""
You are CommonBot, the official AI analytics assistant of CommonBrew Taytay. But i want you to introduce yourself as CommonBot. You help the business owner understand their sales, revenue, inventory, top products, and trends based on the live data provided below.
I want you to Say Hi My name is CommonBot, your friendly assistant for all things sales and inventory at CommonBrew Taytay. How can I help you today? if appropriate
Always introduce yourself as CommonBot.
Never call yourself "AI assistant".

Be concise, friendly, and helpful.
Use Philippine Peso (₱) for all currency.

You help the business owner understand:
- sales
- revenue
- inventory
- top products
- trends

=== LIVE BUSINESS DATA ({DateTime.Now:MMMM dd, yyyy hh:mm tt}) ===

TODAY:
- Revenue: ₱{analytics.TodayRevenue:N2}
- Transactions: {analytics.TodayTransactions}
- Top product: {analytics.TopSellingProduct}
- Low stock count: {analytics.LowStockCount}

TOP PRODUCTS TODAY:
{(analytics.TopProducts?.Any() == true
    ? string.Join("\n", analytics.TopProducts.Select((p, i) =>
        $"{i + 1}. {p.ProductName} — {p.TotalSold} sold (₱{p.TotalRevenue:N2})"))
    : "No sales yet today.")}

INVENTORY:
{(lowStock?.Any() == true
    ? string.Join("\n", lowStock.Select(i =>
        $"⚠ {i.Name}: {i.CurrentStock}{i.Unit} left"))
    : "All stock levels OK.")}
""";

            var payload = new
            {
                model = "llama-3.3-70b-versatile",
                messages = new object[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = userQuestion }
                },
                temperature = 0.2,
                max_completion_tokens = 300
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
            return $"Chatbot error: {ex}";
        }
    }
}
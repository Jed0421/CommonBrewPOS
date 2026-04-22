using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace CommonBrewPOS.Services;

/// <summary>
/// Low-level Supabase REST API client.
/// Uses the service_role key so it bypasses RLS on the backend.
/// </summary>
public class SupabaseService
{
    private readonly HttpClient _http;
    private readonly string _baseUrl;
    private readonly string _serviceKey;
    private readonly JsonSerializerOptions _json = new() { PropertyNameCaseInsensitive = true };

    public SupabaseService(IConfiguration config)
    {
        _baseUrl = config["Supabase:Url"]!.TrimEnd('/');
        _serviceKey = config["Supabase:ServiceKey"] ?? config["Supabase:AnonKey"]!;

        _http = new HttpClient();
        _http.DefaultRequestHeaders.Add("apikey", _serviceKey);
        _http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", _serviceKey);
        _http.DefaultRequestHeaders.Add("Accept", "application/json");
    }

    private HttpRequestMessage BuildRequest(HttpMethod method, string path, object? body = null)
    {
        var req = new HttpRequestMessage(method, $"{_baseUrl}/rest/v1/{path}");
        if (body != null)
        {
            req.Content = new StringContent(
                JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        }
        return req;
    }

    public async Task<List<T>> SelectAsync<T>(string table, string query)
    {
        var response = await _http.GetAsync($"{_baseUrl}/rest/v1/{table}?{query}");
        var body = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            throw new Exception($"Supabase error {(int)response.StatusCode}: {body}");

        return JsonSerializer.Deserialize<List<T>>(body, _json) ?? new List<T>();
    }

    public async Task<T?> SelectSingleAsync<T>(string table, string query)
    {
        var items = await SelectAsync<T>(table, query + "&limit=1");
        return items.FirstOrDefault();
    }

    public async Task<T?> InsertAsync<T>(string table, object data)
    {
        var req = BuildRequest(HttpMethod.Post, table, data);
        req.Headers.Add("Prefer", "return=representation");
        var response = await _http.SendAsync(req);
        var body = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            throw new Exception($"Supabase error {(int)response.StatusCode}: {body}");

        var list = JsonSerializer.Deserialize<List<T>>(body, _json);
        return list != null && list.Count > 0 ? list[0] : default;
    }

    public async Task UpdateAsync(string table, string matchColumn, string matchValue, object data)
    {
        var req = BuildRequest(HttpMethod.Patch, $"{table}?{matchColumn}=eq.{matchValue}", data);
        req.Headers.Add("Prefer", "return=minimal");
        var response = await _http.SendAsync(req);
        var body = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            throw new Exception($"Supabase error {(int)response.StatusCode}: {body}");
    }

    public async Task DeleteAsync(string table, string matchColumn, string matchValue)
    {
        var req = BuildRequest(HttpMethod.Delete, $"{table}?{matchColumn}=eq.{matchValue}");
        var response = await _http.SendAsync(req);
        var body = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            throw new Exception($"Supabase error {(int)response.StatusCode}: {body}");
    }

    public async Task<string> RpcAsync(string functionName, object? args = null)
    {
        var req = new HttpRequestMessage(HttpMethod.Post,
            $"{_baseUrl}/rest/v1/rpc/{functionName}");

        if (args != null)
            req.Content = new StringContent(JsonSerializer.Serialize(args), Encoding.UTF8, "application/json");

        var response = await _http.SendAsync(req);
        var body = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            throw new Exception($"Supabase RPC error {(int)response.StatusCode}: {body}");

        return body;
    }

    public async Task<decimal> GetScalarDecimalAsync(string table, string aggregateQuery)
    {
        var response = await _http.GetAsync($"{_baseUrl}/rest/v1/{table}?{aggregateQuery}");
        var body = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            throw new Exception($"Supabase error {(int)response.StatusCode}: {body}");

        var node = JsonNode.Parse(body);
        if (node is JsonArray arr && arr.Count > 0)
        {
            var first = arr[0];
            foreach (var prop in first!.AsObject())
            {
                if (decimal.TryParse(prop.Value?.ToString(), out var val))
                    return val;
            }
        }

        return 0;
    }
}
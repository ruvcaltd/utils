using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

class Program
{
    static async Task Main()
    {
        string apiKey = "YOUR_OPENAI_API_KEY";
        string endpoint = "https://api.openai.com/v1/chat/completions";

        var requestBody = new
        {
            model = "gpt-3.5-turbo", // or "gpt-4"
            messages = new[]
            {
                new { role = "system", content = "You are a helpful assistant." },
                new { role = "user", content = "Tell me a joke." }
            },
            max_tokens = 100,
            temperature = 0.7
        };

        using var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        var response = await httpClient.PostAsJsonAsync(endpoint, requestBody);

        if (response.IsSuccessStatusCode)
        {
            var responseJson = await response.Content.ReadFromJsonAsync<JsonElement>();
            var message = responseJson
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();

            Console.WriteLine("GPT Response:\n" + message);
        }
        else
        {
            Console.WriteLine($"Error: {response.StatusCode}");
            string errorContent = await response.Content.ReadAsStringAsync();
            Console.WriteLine(errorContent);
        }
    }
}

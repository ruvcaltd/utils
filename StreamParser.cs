using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

public class GraphQLClient
{
    private readonly HttpClient _httpClient;
    private readonly string _endpoint;

    public GraphQLClient(string endpoint, HttpClient httpClient = null)
    {
        _endpoint = endpoint;
        _httpClient = httpClient ?? new HttpClient();
    }

    public async Task<GraphQLResponse<T>> ExecuteQueryAsync<T>(string query, object variables = null)
    {
        var request = new GraphQLRequest
        {
            Query = query,
            Variables = variables
        };

        var json = JsonSerializer.Serialize(request, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync(_endpoint, content);
        var responseContent = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"GraphQL request failed: {response.StatusCode} - {responseContent}");
        }

        var graphQLResponse = JsonSerializer.Deserialize<GraphQLResponse<T>>(responseContent, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        if (graphQLResponse.Errors != null && graphQLResponse.Errors.Length > 0)
        {
            throw new GraphQLException($"GraphQL errors occurred: {string.Join(", ", graphQLResponse.Errors)}");
        }

        return graphQLResponse;
    }
}

// Supporting classes
public class GraphQLRequest
{
    public string Query { get; set; }
    public object Variables { get; set; }
    public string OperationName { get; set; }
}

public class GraphQLResponse<T>
{
    public T Data { get; set; }
    public GraphQLError[] Errors { get; set; }
}

public class GraphQLError
{
    public string Message { get; set; }
    public GraphQLLocation[] Locations { get; set; }
    public string[] Path { get; set; }
}

public class GraphQLLocation
{
    public int Line { get; set; }
    public int Column { get; set; }
}

public class GraphQLException : Exception
{
    public GraphQLException(string message) : base(message) { }
    public GraphQLException(string message, Exception innerException) : base(message, innerException) { }
}

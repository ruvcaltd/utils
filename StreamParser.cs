// Define your response DTOs
public class UserResponse
{
    public User User { get; set; }
}

public class User
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string Email { get; set; }
}

public class CreateUserResponse
{
    public User CreateUser { get; set; }
}

class Program
{
    static async Task Main(string[] args)
    {
        const string endpoint = "https://api.example.com/graphql";
        const string authToken = "your-auth-token";

        // Using the basic client
        using var client = new AdvancedGraphQLClient(endpoint, authToken);

        try
        {
            // Example 1: Query
            var query = @"
                query GetUser($id: ID!) {
                    user(id: $id) {
                        id
                        name
                        email
                    }
                }";

            var variables = new { id = "123" };
            var response = await client.QueryAsync<UserResponse>(query, variables);

            Console.WriteLine($"User: {response.Data.User.Name}");

            // Example 2: Mutation
            var mutation = @"
                mutation CreateUser($input: CreateUserInput!) {
                    createUser(input: $input) {
                        id
                        name
                        email
                    }
                }";

            var mutationVariables = new
            {
                input = new
                {
                    name = "John Doe",
                    email = "john@example.com"
                }
            };

            var mutationResponse = await client.MutateAsync<CreateUserResponse>(mutation, mutationVariables);
            Console.WriteLine($"Created user with ID: {mutationResponse.Data.CreateUser.Id}");

        }
        catch (GraphQLException ex)
        {
            Console.WriteLine($"GraphQL Error: {ex.Message}");
        }
        catch (HttpRequestException ex)
        {
            Console.WriteLine($"HTTP Error: {ex.Message}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
    }
}

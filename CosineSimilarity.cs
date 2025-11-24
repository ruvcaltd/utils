// Project: CosineSimilarityWebAPI
// Single-file minimal API (Program.cs) + README and sample requests included below.

// -----------------------------------------------------------------------------
// README
// -----------------------------------------------------------------------------
// Overview:
// This is a self-contained .NET minimal Web API that demonstrates:
//  - simple deterministic embedding generation (no external services)
//  - storing documents in memory with embeddings
//  - cosine similarity search (ranking)
//  - endpoints: POST /documents, POST /embed, POST /search, GET /documents
//
// Design decisions:
//  - Embeddings use a hashing-to-vector approach: each token is hashed (SHA256) and
//    expanded into a fixed-size vector deterministically. The document embedding is
//    the (normalized) average of token vectors. This is NOT a neural embedding but
//    is deterministic, reproducible, and good enough for demo & local testing.
//  - Cosine similarity implemented in C# with numeric stability.
//  - Everything in-memory for simplicity. Swap in a DB or vector store for production.
//
// How to run:
// 1) Install .NET 8 SDK (or .NET 7 works too).
// 2) Create a folder and save this file as Program.cs in that folder.
// 3) Create a project: `dotnet new web -n CosineSearchApp -o .` (or use `dotnet new webapi`)
//    If you used `dotnet new web`, replace the generated Program.cs with this file's content.
// 4) Run: `dotnet run`
// 5) By default the app listens on http://localhost:5000 (see console output).
//
// Sample requests (curl):
// Add a document:
// curl -X POST http://localhost:5000/documents -H "Content-Type: application/json" -d '{"id":"doc1","text":"The quick brown fox jumps over the lazy dog"}'
// Embed a query text:
// curl -X POST http://localhost:5000/embed -H "Content-Type: application/json" -d '{"text":"brown fox"}'
// Search top 3:
// curl -X POST http://localhost:5000/search -H "Content-Type: application/json" -d '{"query":"quick fox","topK":3}'
// List documents:
// curl http://localhost:5000/documents
//
// Notes:
// - This sample is intended for learning and prototypes only. For production use,
//   replace the embedding algorithm with a trained model or use a vector DB and
//   consider batching, persistence, and nearest-neighbour indexes (HNSW, IVF, etc.).
//
// -----------------------------------------------------------------------------
// Program.cs (C# minimal API)
// -----------------------------------------------------------------------------
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using System.Numerics;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<EmbeddingService>();
builder.Services.AddSingleton<DocumentStore>();

var app = builder.Build();

app.MapGet("/", () => Results.Text("Cosine Similarity Web API is running. See /docs in the canvas for usage."));

app.MapPost("/embed", async (EmbedRequest req, EmbeddingService embedder) =>
{
    if (string.IsNullOrWhiteSpace(req.Text)) return Results.BadRequest("text required");
    var vector = embedder.GetEmbedding(req.Text);
    return Results.Ok(new { embedding = vector });
});

app.MapPost("/documents", async (AddDocumentRequest req, EmbeddingService embedder, DocumentStore store) =>
{
    if (string.IsNullOrWhiteSpace(req.Id)) return Results.BadRequest("id required");
    if (string.IsNullOrWhiteSpace(req.Text)) return Results.BadRequest("text required");

    var embedding = embedder.GetEmbedding(req.Text);
    var doc = new Document { Id = req.Id, Text = req.Text, Embedding = embedding };
    store.AddOrUpdate(doc);
    return Results.Ok(doc);
});

app.MapGet("/documents", (DocumentStore store) => Results.Ok(store.GetAll()));

app.MapPost("/search", (SearchRequest req, EmbeddingService embedder, DocumentStore store) =>
{
    if (string.IsNullOrWhiteSpace(req.Query)) return Results.BadRequest("query required");

    var qEmbedding = embedder.GetEmbedding(req.Query);
    int topK = req.TopK <= 0 ? 5 : req.TopK;

    var results = store.Search(qEmbedding, topK);

    return Results.Ok(new { query = req.Query, topK, results });
});

app.Run();

// -----------------------------------------------------------------------------
// Models & Services
// -----------------------------------------------------------------------------

public record EmbedRequest([property: JsonPropertyName("text")] string Text);
public record AddDocumentRequest([property: JsonPropertyName("id")] string Id, [property: JsonPropertyName("text")] string Text);
public record SearchRequest([property: JsonPropertyName("query")] string Query, [property: JsonPropertyName("topK")] int TopK = 5);

public class Document
{
    public string Id { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public float[] Embedding { get; set; } = Array.Empty<float>();
}

public class DocumentStore
{
    private readonly Dictionary<string, Document> _docs = new();

    public void AddOrUpdate(Document doc)
    {
        _docs[doc.Id] = doc;
    }

    public IReadOnlyCollection<Document> GetAll() => _docs.Values;

    public List<SearchResult> Search(float[] queryEmbedding, int topK)
    {
        var results = new List<SearchResult>();
        foreach (var doc in _docs.Values)
        {
            var score = VectorMath.CosineSimilarity(doc.Embedding, queryEmbedding);
            results.Add(new SearchResult { Id = doc.Id, Text = doc.Text, Score = score });
        }

        return results.OrderByDescending(r => r.Score).Take(topK).ToList();
    }
}

public class SearchResult
{
    public string Id { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public double Score { get; set; }
}

public static class VectorMath
{
    public static double CosineSimilarity(float[] v1, float[] v2)
    {
        if (v1 == null || v2 == null) return 0;
        if (v1.Length != v2.Length) throw new ArgumentException("vector length mismatch");

        // Use double for accumulation
        double dot = 0.0;
        double mag1 = 0.0;
        double mag2 = 0.0;

        int i = 0;
        int n = v1.Length;

        // Optionally use SIMD when vector size is large
        if (Vector.IsHardwareAccelerated && n >= Vector<float>.Count)
        {
            int width = Vector<float>.Count;
            var vDot = new Vector<double>(); // not directly usable - so use scalar accumulation for clarity
        }

        for (i = 0; i < n; i++)
        {
            dot += (double)v1[i] * v2[i];
            mag1 += (double)v1[i] * v1[i];
            mag2 += (double)v2[i] * v2[i];
        }

        double denom = Math.Sqrt(mag1) * Math.Sqrt(mag2);
        if (denom == 0) return 0;
        return dot / denom;
    }
}

public class EmbeddingService
{
    private readonly int _dimension;

    public EmbeddingService(int dimension = 256)
    {
        _dimension = dimension;
    }

    // Public method: get embedding for a piece of text
    public float[] GetEmbedding(string text)
    {
        var tokens = Tokenize(text);
        if (tokens.Count == 0) return new float[_dimension];

        var acc = new double[_dimension];

        foreach (var tok in tokens)
        {
            var vec = TokenVector(tok, _dimension);
            for (int i = 0; i < _dimension; i++) acc[i] += vec[i];
        }

        // average
        int tcount = tokens.Count;
        var result = new float[_dimension];
        double mag = 0.0;
        for (int i = 0; i < _dimension; i++)
        {
            var v = acc[i] / tcount;
            result[i] = (float)v;
            mag += v * v;
        }

        // normalize to unit vector (important for cosine similarity)
        var norm = Math.Sqrt(mag);
        if (norm == 0) return result;
        for (int i = 0; i < _dimension; i++) result[i] = (float)(result[i] / norm);

        return result;
    }

    // Very simple tokenizer: split on whitespace and punctuation, lowercase
    private static List<string> Tokenize(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return new List<string>();
        var sb = new StringBuilder();
        var tokens = new List<string>();
        foreach (var ch in text.ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(ch)) sb.Append(ch);
            else
            {
                if (sb.Length > 0)
                {
                    tokens.Add(sb.ToString());
                    sb.Clear();
                }
            }
        }
        if (sb.Length > 0) tokens.Add(sb.ToString());
        return tokens;
    }

    // Deterministic token -> vector mapping using SHA256 as entropy source.
    // For each token we compute SHA256(token + "|seed") and convert chunks of bytes into floats in [-1,1].
    private static float[] TokenVector(string token, int dimension)
    {
        var output = new float[dimension];
        using (var sha = SHA256.Create())
        {
            var input = Encoding.UTF8.GetBytes(token + "|v1");
            var hash = sha.ComputeHash(input);
            // Expand deterministically by repeatedly hashing (chain) until we have enough bytes
            var bytes = new List<byte>();
            bytes.AddRange(hash);
            var prev = hash;
            while (bytes.Count < dimension * 4)
            {
                prev = sha.ComputeHash(prev);
                bytes.AddRange(prev);
            }

            // Convert each 4-byte group to a float in [-1,1]
            for (int i = 0; i < dimension; i++)
            {
                int idx = i * 4;
                // convert 4 bytes to uint
                uint u = (uint)(bytes[idx] | (bytes[idx + 1] << 8) | (bytes[idx + 2] << 16) | (bytes[idx + 3] << 24));
                // map to [0,1)
                double unit = u / (double)uint.MaxValue;
                // map to [-1,1)
                output[i] = (float)(unit * 2.0 - 1.0);
            }
        }
        return output;
    }
}

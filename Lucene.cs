using Lucene.Net.Analysis.Standard;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.QueryParsers.Classic;
using Lucene.Net.Search;
using Lucene.Net.Store;
using Lucene.Net.Util;
using System.Reflection;
using System.Text.Json;

namespace Lucene
{
    public class ObjectSearchEngine<T> : IDisposable where T : class
    {
        private const LuceneVersion AppLuceneVersion = LuceneVersion.LUCENE_48;
        private readonly RAMDirectory _directory;
        private readonly StandardAnalyzer _analyzer;
        private readonly IndexWriter _writer;
        private readonly List<T> _objects;
        private readonly Dictionary<string, PropertyInfo> _searchableProperties;

        public ObjectSearchEngine(IEnumerable<T> objects)
        {
            _objects = objects.ToList();
            _directory = new RAMDirectory();
            _analyzer = new StandardAnalyzer(AppLuceneVersion);

            var config = new IndexWriterConfig(AppLuceneVersion, _analyzer)
            {
                OpenMode = OpenMode.CREATE
            };
            _writer = new IndexWriter(_directory, config);

            _searchableProperties = GetSearchableProperties();
            IndexObjects();
        }

        private Dictionary<string, PropertyInfo> GetSearchableProperties()
        {
            var properties = new Dictionary<string, PropertyInfo>();
            foreach (var prop in typeof(T).GetProperties())
            {
                var searchableAttr = prop.GetCustomAttribute<Searchable>();
                if (searchableAttr != null)
                {
                    properties[prop.Name] = prop;
                }
            }
            return properties;
        }

        private void IndexObjects()
        {
            for (int i = 0; i < _objects.Count; i++)
            {
                var obj = _objects[i];
                var doc = new Document();

                // Store object index for retrieval
                doc.Add(new StringField("__ObjectIndex", i.ToString(), Field.Store.YES));

                // Serialize the entire object for retrieval
                doc.Add(new StoredField("__ObjectJson", JsonSerializer.Serialize(obj)));

                // Index searchable properties
                foreach (var kvp in _searchableProperties)
                {
                    var propName = kvp.Key;
                    var propInfo = kvp.Value;
                    var value = propInfo.GetValue(obj)?.ToString();

                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        // Store the value for match highlighting
                        doc.Add(new TextField(propName, value, Field.Store.YES));
                        
                        // Add a stored field with the original value
                        doc.Add(new StoredField(propName + "_original", value));
                    }
                }

                _writer.AddDocument(doc);
            }

            _writer.Commit();
            _writer.Flush(triggerMerge: false, applyAllDeletes: false);
        }

        public List<ObjectSearchResult<T>> Search(string searchText, int maxResults)
        {
            if (string.IsNullOrWhiteSpace(searchText))
            {
                return new List<ObjectSearchResult<T>>();
            }

            var allResults = new Dictionary<int, ObjectSearchResult<T>>();

            // Search each property independently
            foreach (var kvp in _searchableProperties)
            {
                var propName = kvp.Key;
                var propInfo = kvp.Value;
                var searchableAttr = propInfo.GetCustomAttribute<Searchable>()!;

                var propertyResults = SearchProperty(propName, searchText, searchableAttr, maxResults * 2);

                foreach (var result in propertyResults)
                {
                    var objIndex = int.Parse(result.Document.Get("__ObjectIndex"));
                    
                    // Calculate match percentage and score
                    var matchPercentage = CalculateMatchPercentage(result.MatchedText, searchText);
                    
                    // Skip if below minimum match percentage
                    if (matchPercentage < searchableAttr.MatchPercentage)
                    {
                        continue;
                    }

                    // Adjust score based on priority (lower priority number = higher boost)
                    var adjustedScore = result.Score * (100f / searchableAttr.Priority);

                    if (allResults.TryGetValue(objIndex, out var existing))
                    {
                        // Keep the best match for this object
                        if (adjustedScore > existing.Score)
                        {
                            allResults[objIndex] = new ObjectSearchResult<T>
                            {
                                Object = _objects[objIndex],
                                MatchedProperty = propName,
                                MatchPercentage = matchPercentage,
                                Score = adjustedScore,
                                MatchedText = result.MatchedText
                            };
                        }
                    }
                    else
                    {
                        allResults[objIndex] = new ObjectSearchResult<T>
                        {
                            Object = _objects[objIndex],
                            MatchedProperty = propName,
                            MatchPercentage = matchPercentage,
                            Score = adjustedScore,
                            MatchedText = result.MatchedText
                        };
                    }
                }
            }

            // Get initial results sorted by score
            var results = allResults.Values
                .OrderByDescending(r => r.Score)
                .Take(maxResults)
                .ToList();

            // Post-processing: Add related issuers based on UltimateIssuerCode
            results = AddRelatedIssuers(results, allResults, maxResults);

            return results;
        }

        private List<PropertySearchResult> SearchProperty(string propertyName, string searchText, Searchable searchableAttr, int maxResults)
        {
            var results = new List<PropertySearchResult>();

            using var reader = DirectoryReader.Open(_directory);
            var searcher = new IndexSearcher(reader);

            var queries = BuildQueriesForProperty(propertyName, searchText, searchableAttr);

            foreach (var query in queries)
            {
                var topDocs = searcher.Search(query, maxResults);

                foreach (var scoreDoc in topDocs.ScoreDocs)
                {
                    var doc = searcher.Doc(scoreDoc.Doc);
                    var matchedText = doc.Get(propertyName) ?? "";

                    results.Add(new PropertySearchResult
                    {
                        Document = doc,
                        Score = scoreDoc.Score,
                        MatchedText = matchedText
                    });
                }

                // If we found results with this query, don't try lower priority queries
                if (results.Any())
                {
                    break;
                }
            }

            return results;
        }

        private List<Query> BuildQueriesForProperty(string propertyName, string searchText, Searchable searchableAttr)
        {
            var queries = new List<Query>();
            var escapedSearchText = QueryParserBase.Escape(searchText);
            var words = searchText.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            if (searchableAttr.ExactMatchOnly)
            {
                // Only exact matches
                var phraseQuery = new PhraseQuery();
                var terms = escapedSearchText.Split(' ');
                foreach (var term in terms)
                {
                    phraseQuery.Add(new Term(propertyName, term.ToLowerInvariant()));
                }
                queries.Add(phraseQuery);
                return queries;
            }

            // Priority 1: Exact phrase match
            var exactPhraseQuery = new PhraseQuery();
            foreach (var word in words)
            {
                exactPhraseQuery.Add(new Term(propertyName, word.ToLowerInvariant()));
            }
            queries.Add(exactPhraseQuery);

            if (words.Length > 1)
            {
                // Priority 2: Same words in exact order but not necessarily adjacent (slop = 2)
                var nearPhraseQuery = new PhraseQuery { Slop = 2 };
                foreach (var word in words)
                {
                    nearPhraseQuery.Add(new Term(propertyName, word.ToLowerInvariant()));
                }
                queries.Add(nearPhraseQuery);

                // Priority 3: All same words in any order
                var allWordsQuery = new BooleanQuery();
                foreach (var word in words)
                {
                    allWordsQuery.Add(new TermQuery(new Term(propertyName, word.ToLowerInvariant())), Occur.MUST);
                }
                queries.Add(allWordsQuery);

                // Priority 4: Many exact words
                var manyWordsQuery = new BooleanQuery();
                foreach (var word in words)
                {
                    manyWordsQuery.Add(new TermQuery(new Term(propertyName, word.ToLowerInvariant())), Occur.SHOULD);
                }
                manyWordsQuery.MinimumNumberShouldMatch = Math.Max(1, (int)Math.Ceiling(words.Length * 0.6));
                queries.Add(manyWordsQuery);

                // Priority 5: Some exact + fuzzy (at least one exact word required)
                var fuzzyWithExactQuery = new BooleanQuery();
                var exactSubQuery = new BooleanQuery();
                foreach (var word in words)
                {
                    exactSubQuery.Add(new TermQuery(new Term(propertyName, word.ToLowerInvariant())), Occur.SHOULD);
                }
                exactSubQuery.MinimumNumberShouldMatch = 1;
                fuzzyWithExactQuery.Add(exactSubQuery, Occur.MUST);

                foreach (var word in words)
                {
                    if (word.Length > 3)
                    {
                        fuzzyWithExactQuery.Add(new FuzzyQuery(new Term(propertyName, word.ToLowerInvariant()), 1), Occur.SHOULD);
                    }
                }
                queries.Add(fuzzyWithExactQuery);
            }
            else if (words.Length == 1)
            {
                // Single word: allow fuzzy but not very sloppy (maxEdits = 1)
                var word = words[0];
                if (word.Length > 3)
                {
                    queries.Add(new FuzzyQuery(new Term(propertyName, word.ToLowerInvariant()), 1));
                }
            }

            return queries;
        }

        private double CalculateMatchPercentage(string matchedText, string searchText)
        {
            if (string.IsNullOrWhiteSpace(matchedText) || string.IsNullOrWhiteSpace(searchText))
            {
                return 0;
            }

            var matchedLower = matchedText.ToLowerInvariant();
            var searchLower = searchText.ToLowerInvariant();

            // Exact match
            if (matchedLower == searchLower)
            {
                return 100;
            }

            // Contains full search text
            if (matchedLower.Contains(searchLower))
            {
                return 90;
            }

            // Calculate based on word matches
            var searchWords = searchLower.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var matchedWords = matchedLower.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            if (searchWords.Length == 0)
            {
                return 0;
            }

            var matchCount = 0;
            foreach (var searchWord in searchWords)
            {
                if (matchedWords.Any(mw => mw.Contains(searchWord) || searchWord.Contains(mw)))
                {
                    matchCount++;
                }
            }

            return (matchCount * 100.0) / searchWords.Length;
        }

        private List<ObjectSearchResult<T>> AddRelatedIssuers(
            List<ObjectSearchResult<T>> results,
            Dictionary<int, ObjectSearchResult<T>> allResults,
            int maxResults)
        {
            // This is specific to CachedIssuer - check if T is CachedIssuer
            if (typeof(T) != typeof(CachedIssuer))
            {
                return results;
            }

            var issuerCodeProp = typeof(T).GetProperty("IssuerCode");
            var ultimateIssuerCodeProp = typeof(T).GetProperty("UltimateIssuerCode");

            if (issuerCodeProp == null || ultimateIssuerCodeProp == null)
            {
                return results;
            }

            var relatedIssuers = new List<ObjectSearchResult<T>>();

            // Get all UltimateIssuerCodes from current results
            var ultimateIssuerCodes = results
                .Select(r => ultimateIssuerCodeProp.GetValue(r.Object)?.ToString())
                .Where(code => !string.IsNullOrWhiteSpace(code))
                .Distinct()
                .ToHashSet();

            // Find issuers whose IssuerCode matches any UltimateIssuerCode
            for (int i = 0; i < _objects.Count; i++)
            {
                // Skip if already in results
                if (allResults.ContainsKey(i))
                {
                    continue;
                }

                var obj = _objects[i];
                var issuerCode = issuerCodeProp.GetValue(obj)?.ToString();

                if (!string.IsNullOrWhiteSpace(issuerCode) && ultimateIssuerCodes.Contains(issuerCode))
                {
                    relatedIssuers.Add(new ObjectSearchResult<T>
                    {
                        Object = obj,
                        MatchedProperty = "UltimateIssuerCode",
                        MatchPercentage = 100,
                        Score = 0.1f, // Low score so they appear after actual matches
                        MatchedText = issuerCode
                    });
                }
            }

            // Combine results
            var combinedResults = results.Concat(relatedIssuers)
                .OrderByDescending(r => r.Score)
                .Take(maxResults)
                .ToList();

            return combinedResults;
        }

        public void Dispose()
        {
            _writer?.Dispose();
            _analyzer?.Dispose();
            _directory?.Dispose();
        }
    }
}

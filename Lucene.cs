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

                    // Calculate position boost (if match is at start of text, boost it)
                    var positionBoost = CalculatePositionBoost(result.MatchedText, searchText);

                    // Adjust score based on priority (lower priority number = higher boost)
                    // Also apply match percentage and position boost
                    var adjustedScore = result.Score 
                        * (100f / searchableAttr.Priority)
                        * ((float)matchPercentage / 100f)
                        * positionBoost;

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

        private float CalculatePositionBoost(string matchedText, string searchText)
        {
            if (string.IsNullOrWhiteSpace(matchedText) || string.IsNullOrWhiteSpace(searchText))
            {
                return 1.0f;
            }

            var matchedLower = matchedText.ToLowerInvariant();
            var searchLower = searchText.ToLowerInvariant();

            // Exact match
            if (matchedLower == searchLower)
            {
                return 1.3f;
            }

            // Starts with search text - highest boost
            if (matchedLower.StartsWith(searchLower))
            {
                return 1.2f;
            }

            // Check if first word matches
            var searchWords = searchLower.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (searchWords.Length > 0)
            {
                var firstSearchWord = searchWords[0];
                if (matchedLower.StartsWith(firstSearchWord))
                {
                    return 1.15f;
                }

                // Find position of first word
                var index = matchedLower.IndexOf(searchLower, StringComparison.Ordinal);
                if (index >= 0)
                {
                    // The earlier in the text, the higher the boost
                    var relativePosition = (float)index / matchedLower.Length;
                    return 1.0f + (0.1f * (1 - relativePosition));
                }
            }

            return 1.0f;
        }

        private List<PropertySearchResult> SearchProperty(string propertyName, string searchText, Searchable searchableAttr, int maxResults)
        {
            var results = new List<PropertySearchResult>();

            using var reader = DirectoryReader.Open(_directory);
            var searcher = new IndexSearcher(reader);

            var queries = BuildQueriesForProperty(propertyName, searchText, searchableAttr);

            // Track which query tier we're using (earlier = better)
            var queryTier = 0;
            
            foreach (var query in queries)
            {
                var topDocs = searcher.Search(query, maxResults);

                foreach (var scoreDoc in topDocs.ScoreDocs)
                {
                    var doc = searcher.Doc(scoreDoc.Doc);
                    var matchedText = doc.Get(propertyName) ?? "";

                    // Apply tier-based boost: first query gets 1.0x, second gets 0.95x, etc.
                    var tierMultiplier = 1.0f - (queryTier * 0.05f);
                    
                    results.Add(new PropertySearchResult
                    {
                        Document = doc,
                        Score = scoreDoc.Score * tierMultiplier,
                        MatchedText = matchedText
                    });
                }

                // If we found results with this query, don't try lower priority queries
                if (results.Any())
                {
                    break;
                }
                
                queryTier++;
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

            // Exact match - 100%
            if (matchedLower == searchLower)
            {
                return 100;
            }

            // Exact match with different case or extra whitespace - 99%
            if (matchedText.Trim().Equals(searchText.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return 99;
            }

            var searchWords = searchLower.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var matchedWords = matchedLower.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            if (searchWords.Length == 0)
            {
                return 0;
            }

            // Single word searches
            if (searchWords.Length == 1)
            {
                var searchWord = searchWords[0];
                
                // Exact word match in multi-word field - 95%
                if (matchedWords.Any(w => w == searchWord))
                {
                    return 95;
                }
                
                // Word starts with search term - 85%
                if (matchedWords.Any(w => w.StartsWith(searchWord)))
                {
                    return 85;
                }
                
                // Contains as substring - 75%
                if (matchedLower.Contains(searchWord))
                {
                    return 75;
                }
                
                // Fuzzy match - calculate edit distance
                var bestMatch = matchedWords
                    .Select(w => new { Word = w, Distance = LevenshteinDistance(searchWord, w) })
                    .OrderBy(x => x.Distance)
                    .FirstOrDefault();
                
                if (bestMatch != null)
                {
                    var similarity = 1.0 - ((double)bestMatch.Distance / Math.Max(searchWord.Length, bestMatch.Word.Length));
                    return Math.Max(50, similarity * 70); // 50-70% range for fuzzy matches
                }
                
                return 50;
            }

            // Multi-word searches
            // Check if it's a phrase match (all words in exact order)
            if (matchedLower.Contains(searchLower))
            {
                // Exact phrase within text - 98%
                if (matchedLower == searchLower)
                {
                    return 100;
                }
                // Phrase at start - 95%
                else if (matchedLower.StartsWith(searchLower))
                {
                    return 95;
                }
                // Phrase at end - 93%
                else if (matchedLower.EndsWith(searchLower))
                {
                    return 93;
                }
                // Phrase in middle - 90%
                else
                {
                    return 90;
                }
            }

            // Check for all words present in order but not adjacent
            if (AreWordsInOrder(matchedLower, searchWords))
            {
                // All words in order - 85%
                return 85;
            }

            // Count exact word matches
            var exactMatches = 0;
            var partialMatches = 0;
            var fuzzyMatches = 0;

            foreach (var searchWord in searchWords)
            {
                if (matchedWords.Any(w => w == searchWord))
                {
                    exactMatches++;
                }
                else if (matchedWords.Any(w => w.StartsWith(searchWord) || searchWord.StartsWith(w)))
                {
                    partialMatches++;
                }
                else if (matchedWords.Any(w => w.Contains(searchWord) || searchWord.Contains(w)))
                {
                    partialMatches++;
                }
                else
                {
                    // Check fuzzy match
                    var hasFuzzyMatch = matchedWords.Any(w => 
                    {
                        var distance = LevenshteinDistance(searchWord, w);
                        var maxLen = Math.Max(searchWord.Length, w.Length);
                        return distance <= Math.Max(1, maxLen / 4); // Allow 25% error
                    });
                    
                    if (hasFuzzyMatch)
                    {
                        fuzzyMatches++;
                    }
                }
            }

            var totalWords = searchWords.Length;
            
            // All words exact match - 82%
            if (exactMatches == totalWords)
            {
                return 82;
            }

            // Calculate weighted percentage
            var exactWeight = 100.0;
            var partialWeight = 60.0;
            var fuzzyWeight = 30.0;

            var weightedScore = (exactMatches * exactWeight + partialMatches * partialWeight + fuzzyMatches * fuzzyWeight) 
                              / (totalWords * exactWeight);

            // Scale to 50-80% range for mixed matches
            var percentage = 50 + (weightedScore * 30);

            return Math.Max(50, percentage);
        }

        private bool AreWordsInOrder(string text, string[] words)
        {
            var currentIndex = 0;
            foreach (var word in words)
            {
                var index = text.IndexOf(word, currentIndex, StringComparison.Ordinal);
                if (index < 0)
                {
                    return false;
                }
                currentIndex = index + word.Length;
            }
            return true;
        }

        private int LevenshteinDistance(string source, string target)
        {
            if (string.IsNullOrEmpty(source))
            {
                return string.IsNullOrEmpty(target) ? 0 : target.Length;
            }

            if (string.IsNullOrEmpty(target))
            {
                return source.Length;
            }

            var sourceLength = source.Length;
            var targetLength = target.Length;
            var distance = new int[sourceLength + 1, targetLength + 1];

            for (var i = 0; i <= sourceLength; i++)
            {
                distance[i, 0] = i;
            }

            for (var j = 0; j <= targetLength; j++)
            {
                distance[0, j] = j;
            }

            for (var i = 1; i <= sourceLength; i++)
            {
                for (var j = 1; j <= targetLength; j++)
                {
                    var cost = target[j - 1] == source[i - 1] ? 0 : 1;
                    distance[i, j] = Math.Min(
                        Math.Min(distance[i - 1, j] + 1, distance[i, j - 1] + 1),
                        distance[i - 1, j - 1] + cost);
                }
            }

            return distance[sourceLength, targetLength];
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

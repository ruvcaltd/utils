using FuzzySharp;
using FuzzySharp.SimilarityRatio;
using JaroWinkler;

public class CompanyRanker
{
    public static double Score(string a, string b)
    {
        double jw = JaroWinklerDistance.JaroWinkler(a, b);              // 0–1
        double tsr = Fuzz.TokenSetRatio(a, b) / 100.0;                  // normalised 0–1
        double lev = 1.0 - (double)Fastenshtein.Levenshtein.Distance(a, b)
                              / Math.Max(a.Length, b.Length);            // 0–1

        return 0.5 * jw + 0.3 * tsr + 0.2 * lev;
    }
}

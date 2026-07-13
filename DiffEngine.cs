using System;
using System.Collections.Generic;

namespace ImageTextComparer
{
    public enum DiffType
    {
        Unchanged,
        Deleted, // Exists in original, not in modified
        Inserted  // Exists in modified, not in original
    }

    public class DiffResult
    {
        public DiffType Type { get; set; }
        public string Text { get; set; }

        public DiffResult(DiffType type, string text)
        {
            Type = type;
            Text = text;
        }

        public override string ToString()
        {
            return $"[{Type}]: {Text}";
        }
    }

    public static class DiffEngine
    {
        /// <summary>
        /// Checks if a character belongs to CJK (Chinese, Japanese, Korean) ranges.
        /// </summary>
        private static bool IsCjk(char c)
        {
            int val = (int)c;
            return (val >= 0x3000 && val <= 0x303F) || // CJK symbols and punctuation
                   (val >= 0x3040 && val <= 0x309F) || // Hiragana
                   (val >= 0x30A0 && val <= 0x30FF) || // Katakana
                   (val >= 0x4E00 && val <= 0x9FFF) || // CJK Unified Ideographs (Kanji)
                   (val >= 0xF900 && val <= 0xFAFF) || // CJK Compatibility Ideographs
                   (val >= 0xFF00 && val <= 0xFFEF);   // Fullwidth forms
        }

        /// <summary>
        /// Tokenizes a string into words, CJK characters, and whitespace blocks.
        /// </summary>
        public static List<string> Tokenize(string text)
        {
            var tokens = new List<string>();
            if (string.IsNullOrEmpty(text)) return tokens;

            int i = 0;
            while (i < text.Length)
            {
                char c = text[i];
                if (char.IsWhiteSpace(c))
                {
                    int start = i;
                    while (i < text.Length && char.IsWhiteSpace(text[i]))
                    {
                        i++;
                    }
                    tokens.Add(text.Substring(start, i - start));
                }
                else if (IsCjk(c))
                {
                    // CJK characters are treated as individual tokens for precise diffing
                    tokens.Add(c.ToString());
                    i++;
                }
                else
                {
                    // Group Latin characters, numbers, and symbols into words
                    int start = i;
                    while (i < text.Length && !char.IsWhiteSpace(text[i]) && !IsCjk(text[i]))
                    {
                        i++;
                    }
                    tokens.Add(text.Substring(start, i - start));
                }
            }
            return tokens;
        }

        /// <summary>
        /// Compares two strings at the token level using Longest Common Subsequence (LCS)
        /// and returns a list of differences.
        /// </summary>
        public static List<DiffResult> Compare(string original, string modified)
        {
            List<string> tokensA = Tokenize(original);
            List<string> tokensB = Tokenize(modified);

            int n = tokensA.Count;
            int m = tokensB.Count;

            int[,] dp = new int[n + 1, m + 1];

            // Build DP table
            for (int i = 1; i <= n; i++)
            {
                for (int j = 1; j <= m; j++)
                {
                    if (tokensA[i - 1] == tokensB[j - 1])
                    {
                        dp[i, j] = dp[i - 1, j - 1] + 1;
                    }
                    else
                    {
                        dp[i, j] = Math.Max(dp[i - 1, j], dp[i, j - 1]);
                    }
                }
            }

            // Backtrack to find diff
            var results = new List<DiffResult>();
            int r = n;
            int c = m;

            while (r > 0 || c > 0)
            {
                if (r > 0 && c > 0 && tokensA[r - 1] == tokensB[c - 1])
                {
                    results.Add(new DiffResult(DiffType.Unchanged, tokensA[r - 1]));
                    r--;
                    c--;
                }
                else if (c > 0 && (r == 0 || dp[r, c - 1] >= dp[r - 1, c]))
                {
                    results.Add(new DiffResult(DiffType.Inserted, tokensB[c - 1]));
                    c--;
                }
                else if (r > 0 && (c == 0 || dp[r, c - 1] < dp[r - 1, c]))
                {
                    results.Add(new DiffResult(DiffType.Deleted, tokensA[r - 1]));
                    r--;
                }
            }

            results.Reverse();
            return results;
        }
    }
}

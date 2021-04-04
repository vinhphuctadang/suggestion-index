using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;    
    
namespace indexer_leveled_kgram {

    class RevertIndexSuggestionEngine : SuggestionEngine {

        struct Occurence {
            int tokenIndex;
            int position;

            public Occurence(int tokenIndex, int position) {
                this.tokenIndex = tokenIndex;
                this.position = position;
            }
        }
        Dictionary<string, List<Occurence>> bigrams;
        Dictionary<int, HashSet<int>> invertedIndex;
        Dictionary<string, int> dict;
        List<string> documents;
        public RevertIndexSuggestionEngine() {
            this.bigrams = new Dictionary<string, List<Occurence>>();
            this.invertedIndex = new Dictionary<int, HashSet<int>>();
            this.dict = new Dictionary<string, int>();
            this.documents = new List<string>();
        }

        Dictionary<string, int> GetBigrams(string s) {
            string delimiter = " .*\"$-";
            var result = new Dictionary<string, int>();

            for(int i = -1; i<s.Length; ++i) {
                string bigram = "";
                if (i == -1) {
                    bigram = "$" + s[i+1].ToString();
                }
                else if (i == s.Length - 1) {
                    bigram = s[i].ToString() + "$";
                }
                else {
                    char c1 = s[i], c2 = s[i+1];
                    if (delimiter.Contains(c1)) { 
                        c1 = '$';
                    }
                    if (delimiter.Contains(c2)) { 
                        c2 = '$';
                    }
                    bigram = c1.ToString() + c2;
                }
                if (bigram == "$$") {
                    continue;
                }
                int count = 0;
                if (!result.TryGetValue(bigram, out count)) {
                    result[bigram] = 0;
                }
                else {
                    result[bigram] ++;
                }
            }
        
            return result;
        }

        override public void Index(string dictionaryName) {
            var docCount = 0;
            var uniqueTokenCount = 0;
            using (var sr = new StreamReader(dictionaryName)) {
                string s = null;
                while ((s = sr.ReadLine()) != null) {
                    s = s.Trim();
                    if (s.Length == 0) {
                        continue;
                    }

                    documents.Add(s);
                    var tokens = s.Split(' ');
                    var tokenIndex = 0;
                    
                    for(int i = 0; i<tokens.Length; ++i) {
                        if (!dict.TryGetValue(tokens[i], out tokenIndex)) {
                            dict[tokens[i]] = ++uniqueTokenCount;
                            tokenIndex = uniqueTokenCount;
                        }

                        HashSet<int> postList;
                        // after tokenization, use token index as key for invertedIndex
                        if (!invertedIndex.TryGetValue(tokenIndex, out postList)) {
                            postList = new HashSet<int>();
                            invertedIndex[tokenIndex] = postList;
                        }

                        // docCount as docIndex
                        postList.Add(docCount);
                    }

                    docCount ++;
                }
            }

            // process tokens:
            foreach(var entry in dict) {
                var token = entry.Key;
                var tokenIndex = entry.Value;

                var gramDict = GetBigrams(token);

                int i = 0;
                foreach(var gramEntry in gramDict) {
                    List<Occurence> tokenIndexSet;
                    if (!bigrams.TryGetValue(gramEntry.Key, out tokenIndexSet)) {
                        tokenIndexSet = new List<Occurence>();
                        bigrams[gramEntry.Key] = tokenIndexSet;
                    }
                    tokenIndexSet.Add(new Occurence(tokenIndex, i++));
                }
            }
        }

        public List<int> SuggestToken(string token) {
            return new List<int>();
        }
        override public SuggestionResult[] GetSuggestions(string query) {

            return new SuggestionResult[0];
        }

        override public long GetIndexSize(){
            return 0;
        }
    }
}
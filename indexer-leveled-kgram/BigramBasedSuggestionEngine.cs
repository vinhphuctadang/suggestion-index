using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;    
    
namespace indexer_leveled_kgram {
    class BigramBasedSuggestionEngine : SuggestionEngine {
            
        List<string> documents;
        Dictionary<string, HashSet<int>> bigramToSuggestions;
        public BigramBasedSuggestionEngine(){
            // this.dict = new Dictionary<string, int>();
            this.documents = new List<string>();
            this.bigramToSuggestions = new Dictionary<string, HashSet<int>>();
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
        
            using (var sr = new StreamReader(dictionaryName)) {
                string s = null;
                while ((s = sr.ReadLine()) != null) {
                    documents.Add(s);
                    var grams = GetBigrams(s);
                    foreach(var entry in grams) {
                        HashSet<int> tmp;
                        if (!bigramToSuggestions.TryGetValue(entry.Key, out tmp)) {
                            tmp = new HashSet<int>();
                            bigramToSuggestions[entry.Key] = tmp;
                        }
                        tmp.Add(docCount);
                    }
                    docCount ++;
                }
            }
        }

        override public SuggestionResult[] GetSuggestions(string query) {
            
            // to bigrams
            var bigrams = GetBigrams(query);
            var aggregated = new Dictionary<int, SuggestionResult>();

            foreach(var gram in bigrams) {
                var gramLabel = gram.Key;
                // Console.WriteLine("label: " + gramLabel);
                HashSet<int> docs;
                if (bigramToSuggestions.TryGetValue(gramLabel, out docs)) {
                    foreach(var docId in docs) {
                        SuggestionResult tempSuggestionResult;
                        // add to aggregated result
                        if (!aggregated.TryGetValue(docId, out tempSuggestionResult)) {
                            aggregated[docId] = new SuggestionResult(documents[docId], 1);
                        }
                        else {
                            tempSuggestionResult.score ++;
                        }
                        continue;
                    }
                }
            }
            return aggregated.Values.OrderBy(x => -x.score).ToArray();
        }
        override public long GetIndexSize() {
            long size = 0;
            foreach(var entry in bigramToSuggestions) {
                size = size + 2 + entry.Value.Count * 4 + 4;
            }
            return size;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace indexer_leveled_kgram {

    class SuggestionResultByIndex {
        public int value;
        public double score;

        public SuggestionResultByIndex(int value, double score) {
            this.value = value;
            this.score = score;
        }
    }

    class InvertedIndexSuggestionEngine : SuggestionEngine {
        // struct Occurence {
        //     public int tokenIndex;
        //     public int position;

        //     public Occurence(int tokenIndex, int position) {
        //         this.tokenIndex = tokenIndex;
        //         this.position = position;
        //     }
        // }
        class SuggestionEntry {
            public int id;
            public string content;
            public int count;
            public SuggestionEntry(int id, string content, int count) {
                this.id = id;
                this.content = content;
                this.count = count;
            }
        }
        Dictionary<string, HashSet<int>> bigrams; // bigram:string => [tokenId:int]
        Dictionary<int, HashSet<int>> invertedIndex;
        Dictionary<string, int> dict;
        Dictionary<int, SuggestionEntry> idToSuggestion;
        Dictionary<string, SuggestionEntry> contentToSuggestion;
        Dictionary<int, string> invertedDict;
        Dictionary<int, HashSet<int>> productIdToSuggestion;

        int uniqueTokenCount;

        public InvertedIndexSuggestionEngine() {
            this.bigrams        = new Dictionary<string, HashSet<int>>();
            this.invertedIndex  = new Dictionary<int, HashSet<int>>();
            this.dict           = new Dictionary<string, int>();
            this.idToSuggestion      = new Dictionary<int, SuggestionEntry>();
            this.contentToSuggestion = new Dictionary<string, SuggestionEntry>();
            this.invertedDict   = new Dictionary<int, string>();
            this.uniqueTokenCount = 0;
            this.productIdToSuggestion = new Dictionary<int, HashSet<int>>();
        }

        public SuggestionResult[] SuggestToken(string pollutedToken, int limit) {
            // naive method
            var grams = GetBigrams(pollutedToken);
            var result = new Dictionary<int, SuggestionResult>();
            foreach(var gram in grams) {
                SuggestionResult suggest;
                HashSet<int> tokenIndexes;
                if (!bigrams.TryGetValue(gram.Key, out tokenIndexes)) {
                    continue;
                }
                foreach(var tokenId in tokenIndexes) {
                    // add candidate words to dict
                    if (!result.TryGetValue(tokenId, out suggest)) {
                        result[tokenId] = new SuggestionResult(invertedDict[tokenId], 1);
                    }
                    else {
                        suggest.score ++;
                    }
                }
            }
            return result.Values.OrderBy(x => -x.score).Take(limit).ToArray();
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

        // add suggestion to forward index
        int AddSuggestionEntry(string suggestion, out bool isNew){
            int id;
            SuggestionEntry entry;
            isNew = !contentToSuggestion.TryGetValue(suggestion, out entry);
            if (isNew) {
                id = contentToSuggestion.Count;
                entry = new SuggestionEntry(id, suggestion, 1);
                contentToSuggestion[suggestion] = idToSuggestion[id] = entry;
            }
            else {
                entry.count ++;
            }

            return entry.id;
        }

        void AddToInvertedIndex(int suggestionId, string suggestion) {
            var tokens = suggestion.Split(' ');
            int tokenIndex;
            for(int i = 0; i<tokens.Length; ++i) {
                if (!dict.TryGetValue(tokens[i], out tokenIndex)) {
                    dict[tokens[i]] = ++uniqueTokenCount;
                    tokenIndex = uniqueTokenCount;
                    invertedDict[uniqueTokenCount] = tokens[i];
                }
                HashSet<int> postList;
                // after tokenization, use token index as key for invertedIndex
                if (!invertedIndex.TryGetValue(tokenIndex, out postList)) {
                    postList = new HashSet<int>();
                    invertedIndex[tokenIndex] = postList;
                }
                postList.Add(suggestionId);
            }
        }

        void AddToProductSuggestions(int productId, int suggestionId) {
            HashSet<int> suggestions;
            if (!productIdToSuggestion.TryGetValue(productId, out suggestions)) {
                productIdToSuggestion[productId] = suggestions = new HashSet<int>();
            }
            suggestions.Add(suggestionId);
        }

        public void Insert(int productId, string suggestion) {
            bool isNew;
            int suggestionId = AddSuggestionEntry(suggestion, out isNew);
            if (isNew) {
                AddToInvertedIndex(suggestionId, suggestion);
            }
            AddToProductSuggestions(productId, suggestionId);
        }

        void RemoveTokenFromIndex(string token){
            int tokenId = dict[token];
            invertedDict.Remove(tokenId);
            invertedIndex.Remove(tokenId);
            dict.Remove(token);

            var grams = GetBigrams(token);
            var removeLater = new List<string>();
            foreach(var gram in grams) {
                HashSet<int> tokenIndexes = bigrams[gram.Key];
                tokenIndexes.Remove(tokenId);

                if (tokenIndexes.Count == 0) {
                    removeLater.Add(gram.Key);
                }
            }
            foreach(var gram in removeLater) {
                bigrams.Remove(gram);
            }
        }

        public void PrintIndex(){
            Console.WriteLine("Documents:");
            foreach(var entry in idToSuggestion) {
              Console.WriteLine(entry.Key + ": " + entry.Value.content);
            }

            Console.WriteLine("Bigrams:");
            foreach(var entry in dict) {
              Console.WriteLine(entry.Key + ": " + String.Join(", ", entry.Value));
            }

            Console.WriteLine("Inverted dictionary:");
            foreach(var entry in invertedIndex) {
              Console.WriteLine(entry.Key + ": " + String.Join(", ", entry.Value));
            }
        }

        public void Delete(int productId){
            foreach(var suggestionId in productIdToSuggestion[productId]) {
                // decrease reference to suggestion
                idToSuggestion[suggestionId].count--;
                if (idToSuggestion[suggestionId].count == 0) {
                    var content = idToSuggestion[suggestionId].content;
                    foreach(var token in content.Split(" ")) {
                        HashSet<int> postList;
                        int tokenId = dict[token];
                        if (invertedIndex.TryGetValue(tokenId, out postList)) {
                            postList.Remove(suggestionId);
                        }

                        if (postList.Count == 0) {
                          // also remove token bigram if there is no more of that gram
                          RemoveTokenFromIndex(token);
                        }
                    }
                    idToSuggestion.Remove(suggestionId);
                }
            }

            productIdToSuggestion.Remove(productId);
        }
        public void GetProductAndSuggestion(string txt, out int productId, out string suggestion) {
            var tokens = txt.Split(' ');
            productId = int.Parse(tokens[0]);
            suggestion = String.Join(" ", tokens.Skip(1));
        }

        override public void Index(string dictionaryName) {
            using (var sr = new StreamReader(dictionaryName)) {
                string s = null;
                while ((s = sr.ReadLine()) != null) {
                    s = s.Trim();
                    if (s.Length == 0) {
                        continue;
                    }
                    int productId;
                    string suggestion;
                    GetProductAndSuggestion(s, out productId, out suggestion);
                    Insert(productId, suggestion);
                }
            }

            // process tokens:
            foreach(var entry in dict) {
                var token = entry.Key;
                var tokenIndex = entry.Value;

                var gramDict = GetBigrams(token);

                // int i = 0;
                foreach(var gramEntry in gramDict) {
                    HashSet<int> tokenIndexes;
                    if (!bigrams.TryGetValue(gramEntry.Key, out tokenIndexes)) {
                        tokenIndexes = new HashSet<int>();
                        bigrams[gramEntry.Key] = tokenIndexes;
                    }
                    tokenIndexes.Add(tokenIndex);
                }
            }

            Console.WriteLine("Number of bigrams: " + bigrams.Count);
        }

        override public SuggestionResult[] GetSuggestions(string query) {
            query = query.Trim();
            // tokenizer
            var tokens = query.Split(' ').ToList();
            var result = new Dictionary<int, SuggestionResult>();

            for(int i = 0; i<tokens.Count; ++i) {
                int tokenIndex;
                if (dict.TryGetValue(tokens[i], out tokenIndex)) {
                    foreach(var candidate in invertedIndex[tokenIndex]) {
                        SuggestionResult suggestion;
                        if (!result.TryGetValue(candidate, out suggestion)){
                            result[candidate] = new SuggestionResult(idToSuggestion[candidate].content, 1);
                        }
                        else {
                            suggestion.score ++;
                        }
                    }

                    continue;
                }

                // else go for a suggestion for 3 small word:
                var suggestions = SuggestToken(tokens[i], 3);
                for(int j = 0; j<suggestions.Length; ++j) {
                    tokens.Add(suggestions[j].value);
                }
            }
            return result.Values.OrderBy(x => -x.score).Take(10).ToArray();
        }

        public SuggestionResult[] GetFastSuggestions(string query, int tolerance = 100) {
            // tokenizer
            var tokens = query.Split(' ').ToList();
            var result = new Dictionary<int, SuggestionResult>();

            for(int i = 0; i<tokens.Count; ++i) {
                int tokenIndex;
                if (dict.TryGetValue(tokens[i], out tokenIndex)) {
                    foreach(var candidate in invertedIndex[tokenIndex]) {
                        SuggestionResult suggestion;
                        if (!result.TryGetValue(candidate, out suggestion)){
                            result[candidate] = new SuggestionResult(idToSuggestion[candidate].content, 1);
                        }
                        else {
                            suggestion.score ++;
                        }
                    }

                    continue;
                }

                var watch = new Stopwatch();
                watch.Start();
                // else go for a suggestion for matches small words:
                var suggestions = SuggestToken(tokens[i], 2);
                watch.Stop();
                Console.WriteLine("Suggestion ellapsed time: " + watch.Elapsed.TotalMilliseconds.ToString("0.0") + "ms");
                for(int j = 0; j<suggestions.Length; ++j) {
                    tokens.Add(suggestions[j].value);
                }
            }
            return result.Values.OrderBy(x => -x.score).Take(10).ToArray();
        }

        override public long GetIndexSize(){
            return 0;
        }
    }
}

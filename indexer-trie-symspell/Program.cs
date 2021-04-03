using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using Gma.DataStructures.StringSearch;
using System.Diagnostics;

namespace indexer
{
    class Program
    {

        class SearchResult {
            public string value;
            public int score;

            public SearchResult(string value, int score) {
                this.value = value;
                this.score = score;
            }
        }
        static void Main(string[] args)
        {

            var path = AppDomain.CurrentDomain.BaseDirectory + "real-suggests.txt";
            var indexerPath = AppDomain.CurrentDomain.BaseDirectory + "real-suggests.index";
            
            // var path = AppDomain.CurrentDomain.BaseDirectory + "all-suggests-cleaned.txt";
            // var indexerPath = AppDomain.CurrentDomain.BaseDirectory + "all-suggests-cleaned.index";
            // var path = AppDomain.CurrentDomain.BaseDirectory + "small-suggests.txt";
            // var indexerPath = AppDomain.CurrentDomain.BaseDirectory + "small-suggests.index";
            var dict = new Dictionary<string, int>();
            var frequency = new Dictionary<string, int>();
            var inverter = new Dictionary<int, HashSet<int>>();
            var documents = new List<string>();
            
            // try indexing by using invert index
            var count = 0;
            var stringIndex = 0;

            Console.WriteLine("Reading and indexing ...");
            long memSize = GC.GetTotalMemory(true);
            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();

            var trie = new PatriciaSuffixTrie<string>(1);
            
            using (var sr = new StreamReader(path)) {
                string s = null;
                while ((s = sr.ReadLine()) != null) {
                    documents.Add(s);
                    foreach(var word in s.Split(' ')) {
                        int tmp;
                        if (!dict.TryGetValue(word, out tmp)) {
                            dict[word] = ++count;
                            frequency[word] = 1;
                        }
                        else {
                            frequency[word]++;
                        }

                        HashSet<int> tmpStringIndexes;
                        if (!inverter.TryGetValue(dict[word], out tmpStringIndexes)) {
                            tmpStringIndexes = new HashSet<int>();
                            inverter[count] = tmpStringIndexes;
                        }
                        tmpStringIndexes.Add(stringIndex);
                    }
                    stringIndex ++;
                }
            }

            Console.WriteLine("Adding to completion dict...");
            memSize = GC.GetTotalMemory(true);
            foreach(var entry in dict) {
                trie.Add(entry.Key, entry.Key);
            }
            Console.WriteLine("Add to completion dict: " + ((GC.GetTotalMemory(true)-memSize) / 1024 / 1024.0).ToString("N0") + " MB. Token count: " + dict.Count);
            
            stopWatch = new Stopwatch();
            stopWatch.Start();
            Console.WriteLine("Adding to symSpell for fast spellCheck");
            memSize = GC.GetTotalMemory(true);
            // dictionary for symspell
            var spellChecker = new SymSpell(dict.Count, 2);
            foreach(var entry in frequency) {
                spellChecker.CreateDictionaryEntry(entry.Key, entry.Value);
            }


            Console.WriteLine("Spell dictionary constructed. " + ((GC.GetTotalMemory(true)-memSize) / 1024 / 1024.0).ToString("N0") + "MB, " + stopWatch.Elapsed.TotalMilliseconds.ToString("0.0") + "ms. Tokens:" + frequency.Count);
            stopWatch = new Stopwatch();
            stopWatch.Start();
            Console.WriteLine("Saving index ...");
            stopWatch.Stop();
            long byteCount = SaveIndex(indexerPath, trie, spellChecker, inverter, dict, documents);

            stopWatch = new Stopwatch();
            Console.WriteLine("File saved: " + byteCount + " bytes. Time ellapsed: " + stopWatch.Elapsed.TotalMilliseconds.ToString("0.0") + "ms");
            stopWatch.Start();
            Console.WriteLine("Searching ...");
            var hits = Search("342 cw", trie, spellChecker, inverter, dict, documents, 10);
            stopWatch.Stop();
            var timeEllapsed = stopWatch.Elapsed.TotalMilliseconds.ToString("0.0");
            // foreach(var hit in hits) {
            //     Console.WriteLine("--> " + hit.value);
            // }

            Console.WriteLine("Searching done." + timeEllapsed + "ms. Hits:" + hits.Length);
        }

        static SearchResult[] Search(
            // input query
            string query, 
            // trie for prefix/infix matching
            PatriciaSuffixTrie<string> trie,
            SymSpell symSpell,
            // inverted index
            Dictionary<int, HashSet<int>> inverter, 
            // word -> its order
            Dictionary<string, int> dict,
            // collection of documents
            List<string> documents,
            // limit
            int limit
        ) {
            var aggregated = new Dictionary<int, SearchResult>();
            var tokens = new LinkedList<string>();
            foreach(var word in query.ToLower().Split(' ')) {
                tokens.AddLast(word);
            }

            while (tokens.Count > 0) {
                
                // pop_front the queue
                var word = tokens.First.Value;
                tokens.RemoveFirst();
                // pipeline:
                // 1. find exact matches first
                int tmp;
                if (dict.TryGetValue(word, out tmp)) {

                    var docs = inverter[tmp];
                    foreach(var doc in docs) {
                        SearchResult tempSearchResult;
                        // add to aggregated result
                        if (!aggregated.TryGetValue(doc, out tempSearchResult)) {
                            aggregated[doc] = new SearchResult(documents[doc], 1);;
                        }
                        else {
                            tempSearchResult.score ++;
                        }
                    }    
                    continue;
                }

                // if no exact match then search for prefix suggestions (for prefix <= 3)
                if (word.Length <= 3) { // find prefix matches
                    string suggestion = null;
                    // take 1 suggestion first
                    foreach(var suggest in trie.Retrieve(word)) {
                        suggestion = suggest;
                        Console.WriteLine("Prefix matched: " + suggestion);
                        break;
                    }
                    if (suggestion != null) {
                        // push_front
                        tokens.AddFirst(suggestion);
                        continue;
                    }
                }
                
                // if no prefix suggestion found then correct spelling
                var lookupResult = symSpell.LookupCompound(word)[0].term.Split(' ');
                for(int i = lookupResult.Length - 1; i >= 0; --i) {
                    tokens.AddFirst(lookupResult[i]);
                }
            }

            // then sort??
            return aggregated.Values.ToArray();
        }

        // static void GetIndex(
        //     string indexerPath, 
        //     out PatriciaSuffixTrie<string> trie,
        //     out SymSpell symSpell, 
        //     out Dictionary<int, HashSet<int>> inverter, 
        //     out Dictionary<string, int> dict, 
        //     List<string> documents
        // ){

        // }
        static long SaveIndex(string indexerPath, PatriciaSuffixTrie<string> trie,
            SymSpell symSpell, Dictionary<int, HashSet<int>> inverter, Dictionary<string, int> dict, List<string> documents) {

            long fileSize = 0;
            using (var fs = new FileStream(indexerPath, FileMode.Create))
            using (var sw = new BinaryWriter(fs, Encoding.UTF8)) {

                foreach(var entry in dict) {
                    sw.Write(BitConverter.GetBytes(entry.Key.Length));
                    sw.Write(Encoding.ASCII.GetBytes(entry.Key));
                    
                    fileSize += 4 + entry.Key.Length; // 4 + wordLength bytes
                }

                foreach(var phrase in documents){
                    var tokens = phrase.Split(' ');
                    sw.Write(BitConverter.GetBytes(tokens.Length));
                    fileSize += (tokens.Length + 1) * 4; // 4 + tokens.Length * 4
                    foreach(var token in tokens) {
                        var tokenId = dict[token];
                        sw.Write(BitConverter.GetBytes(tokenId));
                    }
                }

                foreach(var entry in inverter) {
                    fileSize += 8 + entry.Value.Count*4;
                    sw.Write(BitConverter.GetBytes(entry.Key));
                    sw.Write(BitConverter.GetBytes(entry.Value.Count));
                    foreach(var referenceIndex in entry.Value) {
                        sw.Write(BitConverter.GetBytes(referenceIndex));
                    }
                }
            }
            return fileSize;
        }


        static Dictionary<string, List<int>> GetBigramIndex(Dictionary<string, int> dict){
            var bigramIndex = new Dictionary<string, List<int>>();
            foreach(var entry in dict) {
                var word = entry.Key;
                var wordIndex = entry.Value;
                var n = word.Length;
                for(int i = -1; i<n; ++i) {
                    string s = null;
                    if (i < 0) {
                        s = "$" + word[0];
                    }
                    else if (i == n - 1) {
                        s = word[i] + "$";
                    }
                    else {
                        s = word[i].ToString() + word[i+1].ToString();
                    }

                    List<int> tmp;
                    if (!bigramIndex.TryGetValue(s, out tmp)) {
                        tmp = new List<int>();
                        bigramIndex[s] = tmp;
                    }
                    tmp.Add(wordIndex);
                }
            }

            return bigramIndex;
        }
    }
}

// https://towardsdatascience.com/the-pruning-radix-trie-a-radix-trie-on-steroids-412807f77abc
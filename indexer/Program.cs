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
            var path = AppDomain.CurrentDomain.BaseDirectory + "all-suggests-cleaned.txt";
            var indexerPath = AppDomain.CurrentDomain.BaseDirectory + "all-suggests-cleaned.index";
            // var path = AppDomain.CurrentDomain.BaseDirectory + "small-suggests.txt";
            // var indexerPath = AppDomain.CurrentDomain.BaseDirectory + "small-suggests.index";
            var counter = new Dictionary<string, int>();
            var inverter = new Dictionary<int, HashSet<int>>();
            var stringList = new List<string>();
            
            // try indexing by using invert index
            var count = 0;
            var stringIndex = 0;

            Console.WriteLine("Reading and indexing ...");
            long memSize = GC.GetTotalMemory(true);
            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();

            var trie = new PatriciaSuffixTrie<string>(1);
            
            using (var sr = new StreamReader(path)) {
                string s;

                while ((s = sr.ReadLine()) != null) {

                    stringList.Add(s);

                    foreach(var word in s.Split(' ')) {
                        int tmp;
                        if (!counter.TryGetValue(word, out tmp)) {
                            counter[word] = ++count;         
                        }

                        HashSet<int> tmpStringIndexes;
                        if (!inverter.TryGetValue(counter[word], out tmpStringIndexes)) {
                            tmpStringIndexes = new HashSet<int>();
                            inverter[count] = tmpStringIndexes;
                        }
                        tmpStringIndexes.Add(stringIndex);
                    }
                    stringIndex ++;
                }
            }
            Console.WriteLine("Adding words to trie ...");
            // add word to trie
            memSize = GC.GetTotalMemory(true);
            foreach(var entry in counter) {
                trie.Add(entry.Key, entry.Key);
            }

            Console.WriteLine("Add to trie: " + ((GC.GetTotalMemory(true)-memSize) / 1024 / 1024.0).ToString("N0") + " MB. Token count: " + counter.Count);
            stopWatch.Stop();
            long memDeltaForStoringValues = GC.GetTotalMemory(true) - memSize;
            Console.WriteLine("Everything done in " + stopWatch.Elapsed.TotalMilliseconds.ToString("0.0") + "ms "
                + (memDeltaForStoringValues / 1024 / 1024.0).ToString("N0") + " MB. Token count: " + counter.Count);

            stopWatch = new Stopwatch();
            stopWatch.Start();
            var result = Search("bitcurious", trie, inverter, counter, stringList, 10);
            stopWatch.Stop();

            Console.WriteLine("Search done in " + stopWatch.Elapsed.TotalMilliseconds.ToString("0.0") + "ms, hits: " + result.Count);
            
        }

        static Dictionary<int, SearchResult> Search(string query, PatriciaSuffixTrie<string> trie, Dictionary<int, HashSet<int>> inverter, Dictionary<string, int> counter, List<string> stringList, int limit) {
            var indexToResult = new Dictionary<int, SearchResult>();
            var tokens = query.ToLower().Split(' ').ToList();
            
            bool forceStop = false;
            for(int i = 0; i < tokens.Count; ++i) {
                var word = tokens[i];
                int wordIndex = 0;
                // not hit? try to fix that
                if (!counter.TryGetValue(word, out wordIndex)) {
                    // Fix the word
                    foreach(var suggest in trie.Retrieve(word)) {
                        tokens[0] = suggest;
                        --i;
                        break;
                    }
                    continue;
                }

                // Console.WriteLine("Finding word = " + word + ", Hits: " + String.Join(", ", inverter[wordIndex]));
                // hits
                foreach(var index in inverter[wordIndex]) {
                    SearchResult tmp;

                    // skip, use the top most
                    // if (indexToResult.Count >= limit) {
                    //     forceStop = true;
                    //     break;
                    // }

                    if (!indexToResult.TryGetValue(index, out tmp)) {
                        tmp = new SearchResult(stringList[index], (tokens.Count - i)*(tokens.Count - i));
                        indexToResult[index] = tmp;
                    }
                    else {
                        tmp.score += (tokens.Count - i)*(tokens.Count - i);
                    }
                }

                if (forceStop) {
                    break;
                }
            }
            return indexToResult; // indexToResult.Values.OrderBy(x => x.score).ToArray();
        }

        static long SaveIndex(string indexerPath, Dictionary<int, HashSet<int>> inverter, Dictionary<string, int> counter, List<string> stringList) {

            long fileSize = 0;
            using (var fs = new FileStream(indexerPath, FileMode.Create))
            using (var sw = new BinaryWriter(fs, Encoding.UTF8)) {

                foreach(var entry in counter) {
                    sw.Write(BitConverter.GetBytes(entry.Key.Length));
                    sw.Write(Encoding.ASCII.GetBytes(entry.Key));
                    
                    fileSize += 4 + entry.Key.Length; // 4 + wordLength bytes
                }

                foreach(var phrase in stringList){
                    var tokens = phrase.Split(' ');
                    sw.Write(BitConverter.GetBytes(tokens.Length));
                    fileSize += (tokens.Length + 1) * 4; // 4 + tokens.Length * 4
                    foreach(var token in tokens) {
                        var tokenId = counter[token];
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
    }
}

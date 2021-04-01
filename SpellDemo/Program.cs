using System;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using Gma.DataStructures.StringSearch;
using System.Runtime.Serialization;

namespace CustomSpell
{
    class Program
    {
        //Load a frequency dictionary or create a frequency dictionary from a text corpus
        public static void Main(string[] args)
        {
            var path = AppDomain.CurrentDomain.BaseDirectory + @"all-suggests-cleaned.txt";
            Console.Write("Creating trie ...");
            long memSize = GC.GetTotalMemory(true);
            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();
            var wordToIndex = new Dictionary<string, int>();
            var wordFrequency = new Dictionary<string, int>();
            var phraseList = new List<string>();
            int count = 0;

            using (StreamReader sr = new StreamReader(path))
            {
                while (sr.Peek() >= 0)
                {
                    var s = sr.ReadLine();
                    phraseList.Add(s.Trim());

                    var tokens = s.Trim().Split(' ');
                    
                    for(int i = 0; i<tokens.Length; ++i) {
                        int index = 0, freq = 0;
                        if (!wordToIndex.TryGetValue(tokens[i], out index)) {
                            wordToIndex[tokens[i]] = count++;
                        }
                        if (!wordFrequency.TryGetValue(tokens[i], out freq)) {
                            wordFrequency[tokens[i]] = 1;
                        }
                        else {
                            wordFrequency[tokens[i]] = freq + 1;
                        }
                    }
                }
            }

            long memDeltaForStoringValues = GC.GetTotalMemory(true) - memSize;
            Console.WriteLine("Memory for storing value: " + memDeltaForStoringValues + ". Going to add to trie");

            var trie = new UkkonenTrie<int>(1);
            int value = 0;
            foreach(var phrase in phraseList){
                trie.Add(phrase, value++);
            }

            //Load a frequency dictionary
            stopWatch.Stop();
            long memDelta = GC.GetTotalMemory(true) - memSize;
            Console.WriteLine("Done in " + stopWatch.Elapsed.TotalMilliseconds.ToString("0.0") + "ms "
                + (memDelta / 1024 / 1024.0).ToString("N0") + " MB. Token count: " + wordToIndex.Count);

            // spell checker
            var spellChecker = new SymSpell(wordToIndex.Count, 2);
            foreach(var entry in wordFrequency) {
                spellChecker.CreateDictionaryEntry(entry.Key, entry.Value);
            }
            
            while (true) {
                Console.WriteLine("Input string to search:");
                var s = Console.ReadLine();
                if (s == "exit") { return; }

                var normalized = s.ToLower();
                var suggests = spellChecker.LookupCompound(normalized, 2);
                
                // lookup in trie 
                var results = trie.Retrieve(normalized);

                var resultCount = 0;
                foreach(var result in results) {
                    Console.WriteLine("--> " + phraseList[result]);
                    resultCount++;
                }

                var suggest = suggests[0].term;
                foreach(var sug in suggests) {
                    Console.WriteLine("Can search for: " + sug.term);
                }
                if (suggest != normalized) {
                    Console.WriteLine("Did you mean: " + suggest  + "?");
                }

                Console.WriteLine(String.Format("Found {0} result", resultCount));
            }
        }
    }
}

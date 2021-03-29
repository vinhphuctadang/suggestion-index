using System;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using Gma.DataStructures.StringSearch.Word;

namespace CustomSpell
{
    class Program
    {
        //Load a frequency dictionary or create a frequency dictionary from a text corpus
        public static void Main(string[] args)
        {
            var path = AppDomain.CurrentDomain.BaseDirectory + @"../../../all-suggests-cleaned.txt";
            // var path = AppDomain.CurrentDomain.BaseDirectory + @"../../../small-suggests.txt";
            Console.WriteLine("Creating trie for searching...");
            long memSize = GC.GetTotalMemory(true);
            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();
            var wordToIndex = new Dictionary<string, int>();
            var wordCount = new Dictionary<string, int>();
            var phraseList = new List<string>();
            // var tmp = ""; 
            int count = 0;

            using (StreamReader sr = new StreamReader(path))
            {
                while (sr.Peek() >= 0)
                {
                    var s = sr.ReadLine();
                    phraseList.Add(s.Trim());

                    var tokens = s.Trim().Split(' ');
                    
                    for(int i = 0; i<tokens.Length; ++i) {
                        int index = 0;
                        if (!wordToIndex.TryGetValue(tokens[i], out index)) {
                            wordToIndex[tokens[i]] = count++;
                        }

                        int currentFrequency = 0;
                        if (!wordCount.TryGetValue(tokens[i], out currentFrequency)) {
                            wordCount[tokens[i]] = 1;
                        }
                        else {
                            wordCount[tokens[i]] = currentFrequency + 1;
                        }
                    }
                }
            }

            long memDeltaForStoringValues = GC.GetTotalMemory(true) - memSize;
            Console.WriteLine("Memory for storing value: " + memDeltaForStoringValues + ". Going to add to trie");

            var trie = new UkkonenTrieWord<int>(1, wordToIndex);
            int value = 0;
            foreach(var phrase in phraseList){
                trie.Add(phrase, value++);
            }    
            stopWatch.Stop();
            long memDelta = GC.GetTotalMemory(true) - memSize;
            Console.WriteLine("Done in " + stopWatch.Elapsed.TotalMilliseconds.ToString("0.0") + "ms "
                + (memDelta / 1024 / 1024.0).ToString("N0") + " MB. Token count: " + wordToIndex.Count);
        
            // build spell checker
            var spellChecker = new SymSpell(wordToIndex.Count, 2);
            foreach(var entry in wordCount) {
                spellChecker.CreateDictionaryEntry(entry.Key, entry.Value);
            }

            while(true) {
                Console.WriteLine("Please input string:");
                var s = Console.ReadLine();
                var normalized = s.ToLower();

                if (normalized == "exit") {
                    // terminate on input=="exit"
                    return;
                }

                var suggestions = spellChecker.LookupCompound(normalized);
                var suggestion = suggestions[0].term;
                foreach(var tmp in suggestions) {
                    Console.WriteLine("Correct spell may be: " + tmp.term);
                }
                if (suggestion != normalized) {
                    Console.WriteLine("Did you mean: " + suggestion);
                }
                
                stopWatch = new Stopwatch();
                stopWatch.Start();
                var result = trie.Retrieve(suggestion);

                stopWatch.Stop();
                Console.WriteLine("Done searching in " + stopWatch.Elapsed.TotalMilliseconds.ToString("0.0") + "ms ");
            
                foreach(var res in result) {
                    Console.WriteLine("Suggestions: " + phraseList[res]);
                }
            }
        }
    }
}

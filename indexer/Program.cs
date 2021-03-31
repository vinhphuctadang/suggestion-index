using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using Gma.DataStructures.StringSearch;
using System.Diagnostics;

namespace indexer
{
    class Program
    {


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

            var trie = new UkkonenTrie<string>(1);

            using (var sr = new StreamReader(path)) {
                string s;

                while ((s = sr.ReadLine()) != null) {
                    // Console.WriteLine(stringIndex + ". " + s);
                    foreach(var word in s.Split(' ')) {
                        int tmp;
                        if (!counter.TryGetValue(word, out tmp)) {
                            counter[word] = ++count;
                            trie.Add(word, word);                
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

            long memDeltaForStoringValues = GC.GetTotalMemory(true) - memSize;
            Console.WriteLine("Done in " + stopWatch.Elapsed.TotalMilliseconds.ToString("0.0") + "ms "
                + (memDeltaForStoringValues / 1024 / 1024.0).ToString("N0") + " MB. Token count: " + counter.Count);
            // foreach(var val in trie.Retrieve("ul")) {

            //     Console.WriteLine("val: " + val);
            // }
            Console.WriteLine("Writing index to file ...");

            var fileSize = 0;
            using (var fs = new FileStream(indexerPath, FileMode.Create))
            using (var sw = new BinaryWriter(fs, Encoding.UTF8)) {
                foreach(var entry in inverter) {
                    fileSize += 8 + entry.Value.Count*4;
                    sw.Write(BitConverter.GetBytes(entry.Key));
                    sw.Write(BitConverter.GetBytes(entry.Value.Count));
                    foreach(var referenceIndex in entry.Value) {
                        sw.Write(BitConverter.GetBytes(referenceIndex));
                    }
                }
            }

            Console.WriteLine("Index file size: " +  (fileSize / 1024 / 1024.0).ToString("N0") + "MB");
        }
    }
}

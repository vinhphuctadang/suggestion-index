using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Diagnostics;

namespace indexer_leveled_kgram
{

    class Program
    {
        struct Measurement {
            public long memoryMarker;
            public Stopwatch stopwatch;

            public Measurement(long memoryMarker, Stopwatch stopwatch) {
                this.memoryMarker = memoryMarker;
                this.stopwatch = stopwatch;
            }
        }
    
        static Measurement Mark(){
            var obj = new Measurement(GC.GetTotalMemory(true), new Stopwatch());
            obj.stopwatch.Start();
            return obj;
        }

        static void Report(Measurement m, string prompt){

            m.stopwatch.Stop();
            Console.WriteLine(prompt + 
                " - Time ellapsed: " + m.stopwatch.Elapsed.TotalMilliseconds.ToString("0.0") + "ms " + 
                "Memory = " +  (GC.GetTotalMemory(true) - m.memoryMarker) / (1024*1024) + " MB");
        }

        static void Main(string[] args)
        {
            // tokenization
            var path = AppDomain.CurrentDomain.BaseDirectory + "small-suggests.txt";
            var indexerPath = AppDomain.CurrentDomain.BaseDirectory + "small-suggests.index";
            // var path = AppDomain.CurrentDomain.BaseDirectory + "real-suggests.txt";
            // var indexerPath = AppDomain.CurrentDomain.BaseDirectory + "real-suggests.index";
            var engine = new RevertIndexSuggestionEngine();
            Measurement marker;

            Console.WriteLine("Indexing ...");
            marker = Mark();

            engine.Index(path);
            Report(marker, "Indexing done");

            
            marker = Mark();
            var result = engine.SuggestToken("mensuitesku", 3);
            Report(marker, "Suggestion done. Hit count: " + result.Length);

            int count = 0;
            foreach(var r in result) {
                if (++count > 10) { break; }
                Console.WriteLine(r.value + ", score: " + r.score);
            }
        }
    }
}


// Reference:

// Information Retrieval and Web Search Dictionaries and tolerant retrieval (IIR 3): 
// --> https://michael.hahsler.net/SMU/CSE7337/slides/03dict.pdf

// A query suggestion method combining TF-IDF and Jaccard Coefficient for interactive web search: 
// --> https://core.ac.uk/download/pdf/74375309.pdf

// Idea for Auto-Complete
// --> https://zhangruochi.com/Auto-Complete/2020/07/19/

namespace indexer_leveled_kgram {

    class SuggestionResult {
        public string value;
        public double score;

        public SuggestionResult(string value, double score) {
            this.value = value;
            this.score = score;
        }
    }

    abstract class SuggestionEngine {
        abstract public void Index(string dictionaryName);
        abstract public SuggestionResult[] GetSuggestions(string query);

        abstract public long GetIndexSize();
    }
}
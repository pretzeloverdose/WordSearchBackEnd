namespace FuzzySearch.Models
{
    public class WordEntry
    {
        public WordEntry(object word, object freq, int lower, object len)
        {
            Word = word;
            Freq = freq;
            Lower = lower;
            Len = len;
        }

        public int Id { get; set; }
        public string word { get; set; } = string.Empty;
        public object Word { get; }
        public int Length { get; set; }
        public DateTime CreatedAt { get; set; }
        public object Freq { get; }
        public int Lower { get; }
        public object Len { get; }
        public long Frequency { get; internal set; }
    }
}

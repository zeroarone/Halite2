namespace Halite2.hlt
{
    public class Metadata
    {
        private int index;
        private readonly string[] metadata;

        public Metadata(string[] metadata) { this.metadata = metadata; }

        public string Pop() { return metadata[index++]; }

        public bool IsEmpty() { return index == metadata.Length; }
    }
}
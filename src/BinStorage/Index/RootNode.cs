using System.Collections.Generic;

namespace BinStorage.Index
{
    public class RootNode
    {
        public RootNode()
        {
            Nodes = new Dictionary<string, Node>();
        }

        public IDictionary<string, Node> Nodes { get; set; }
    }
}

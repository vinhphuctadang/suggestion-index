using System.Collections.Generic;
using System.IO;

namespace Gma.DataStructures.StringSearch.Word
{
    internal class Edge<T>
    {
        public Edge(List<int> label, Node<T> target)
        {
            this.Label = label;
            this.Target = target;
        }

        public List<int> Label { get; set; }

        public Node<T> Target { get; private set; }
    }
}
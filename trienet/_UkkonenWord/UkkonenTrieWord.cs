using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;

/**
* vinhphuctadang
* vinhphuctadang@gmail.com
* Optimize for almost repeated suggestions
**/

namespace Gma.DataStructures.StringSearch.Word
{
    public class UkkonenTrieWord : ITrie<int>
    {
        private readonly int _minSuffixLength;

        //The root of the suffix tree
        private readonly Node<int> _root;

        //The last leaf that was added during the update operation
        private Node<int> _activeLeaf;

        private Dictionary<string, int> _wordToIndex;

        public UkkonenTrieWord() : this(0, null)
        {
        }

        public UkkonenTrieWord(int minSuffixLength, Dictionary<string, int> wordToIndex) 
        {
            _minSuffixLength = minSuffixLength;
            _root = new Node<int>();
            _activeLeaf = _root;
            _wordToIndex = wordToIndex;
        }

        public IEnumerable<int> Retrieve(List<int> word)
        {
            if (word.Count < _minSuffixLength) return Enumerable.Empty<int>();
            var tmpNode = SearchNode(word);
            return tmpNode == null 
                ? Enumerable.Empty<int>() 
                : tmpNode.GetData();
        }

        public IEnumerable<int> Retrieve(string key)
        {
            var words = key.Split(' ');
            var newKey = new List<int>();
            for(int i = 0; i<words.Length; ++i) newKey.Add(_wordToIndex[words[i]]);
            return this.Retrieve(newKey);
        }

        private static bool RegionMatches(List<int> first, int toffset, List<int> second, int ooffset, int len)
        {
            for (var i = 0; i < len; i++)
            {
                var one = first[toffset + i];
                var two = second[ooffset + i];
                if (one != two) return false;
            }
            return true;
        }

        /**
         * Returns the tree NodeA<int> (if present) that corresponds to the given string.
         */
        private Node<int> SearchNode(List<int> word)
        {
            /*
             * Verifies if exists a path from the root to a NodeA<int> such that the concatenation
             * of all the labels on the path is a superstring of the given word.
             * If such a path is found, the last NodeA<int> on it is returned.
             */
            var currentNode = _root;

            for (var i = 0; i < word.Count; ++i)
            {
                var ch = word[i];
                // follow the EdgeA<int> corresponding to this char
                var currentEdge = currentNode.GetEdge(ch);
                if (null == currentEdge)
                {
                    // there is no EdgeA<int> starting with this char
                    return null;
                }
                var label = currentEdge.Label;
                var lenToMatch = Math.Min(word.Count - i, label.Count);

                if (!RegionMatches(word, i, label, 0, lenToMatch))
                {
                    // the label on the EdgeA<int> does not correspond to the one in the string to search
                    return null;
                }

                if (label.Count >= word.Count - i)
                {
                    return currentEdge.Target;
                }
                // advance to next NodeA<int>
                currentNode = currentEdge.Target;
                i += lenToMatch - 1;
            }

            return null;
        }

        public void Add(string key, int value)
        {
            var words = key.Split(' ');
            var newKey = new List<int>();
            for(int i = 0; i<words.Length; ++i) newKey.Add(_wordToIndex[words[i]]);
            this.Add(newKey, value);
        }

        public void Add(List<int> key, int value) {
            // reset activeLeaf
            _activeLeaf = _root;

            var remainder = key;
            var s = _root;

            // proceed with tree construction (closely related to procedure in
            // Ukkonen's paper)
            var text = new List<int>();
            // iterate over the string, one char at a time
            for (var i = 0; i < remainder.Count; i++)
            {
                // line 6
                // text += remainder[i];
                text.Add(remainder[i]);
                // use intern to make sure the resulting string is in the pool.
                //TODO Check if needed
                //text = text.Intern();

                // line 7: update the tree with the new transitions due to this new char
                var active = Update(s, text, remainder.Skip(i).ToList(), value);
                // line 8: make sure the active Tuple is canonical
                active = Canonize(active.Item1, active.Item2);

                s = active.Item1;
                text = active.Item2;
            }

            // add leaf suffix link, is necessary
            if (null == _activeLeaf.Suffix && _activeLeaf != _root && _activeLeaf != s)
            {
                _activeLeaf.Suffix = s;
            }
        }
        /**
         * Tests whether the string stringPart + t is contained in the subtree that has inputs as root.
         * If that's not the case, and there exists a path of edges e1, e2, ... such that
         *     e1.label + e2.label + ... + $end = stringPart
         * and there is an EdgeA<int> g such that
         *     g.label = stringPart + rest
         * 
         * Then g will be split in two different edges, one having $end as label, and the other one
         * having rest as label.
         *
         * @param inputs the starting NodeA<int>
         * @param stringPart the string to search
         * @param t the following character
         * @param remainder the remainder of the string to add to the index
         * @param value the value to add to the index
         * @return a Tuple containing
         *                  true/false depending on whether (stringPart + t) is contained in the subtree starting in inputs
         *                  the last NodeA<int> that can be reached by following the path denoted by stringPart starting from inputs
         *         
         */

        private static bool Equals(List<int> a, List<int> b){
            if (a.Count != b.Count) return false;

            for(int i = 0; i<a.Count; ++i) {
                if (a[i] != b[i]) return false;
            }
            return true;
        }


        // refactored
        private static Tuple<bool, Node<int>> TestAndSplit(Node<int> inputs, List<int> stringPart, int t, List<int> remainder, int value)
        {
            // descend the tree as far as possible
            var ret = Canonize(inputs, stringPart);
            var s = ret.Item1;
            var str = ret.Item2;

            // if (!(string.Empty.Equals(str)))
            if (str.Count != 0)
            {
                var g = s.GetEdge(str[0]);

                var label = g.Label;
                // must see whether "str" is substring of the label of an EdgeA<int>
                if (label.Count > str.Count && label[str.Count] == t)
                {
                    return new Tuple<bool, Node<int>>(true, s);
                }
                // need to split the EdgeA<int>
                var newlabel = label.Skip(str.Count).ToList();
                //assert (label.startsWith(str));

                // build a new NodeA<int>
                var r = new Node<int>();
                // build a new EdgeA<int>
                var newedge = new Edge<int>(str, r);

                g.Label = newlabel;

                // link s -> r
                r.AddEdge(newlabel[0], g);
                s.AddEdge(str[0], newedge);

                return new Tuple<bool, Node<int>>(false, r);
            }
            var e = s.GetEdge(t);
            if (null == e)
            {
                // if there is no t-transtion from s
                return new Tuple<bool, Node<int>>(false, s);
            }
            // if (remainder.Equals(e.Label))
            if (Equals(remainder, e.Label))
            {
                // update payload of destination NodeA<int>
                e.Target.AddRef(value);
                return new Tuple<bool, Node<int>>(true, s);
            }
            // if (remainder.StartsWith(e.Label))
            if (StartsWith(remainder, e.Label))
            {
                return new Tuple<bool, Node<int>>(true, s);
            }
            if (!StartsWith(e.Label, remainder))
            {
                return new Tuple<bool, Node<int>>(true, s);
            }
            // need to split as above
            var newNode = new Node<int>();
            newNode.AddRef(value);

            var newEdge = new Edge<int>(remainder, newNode);
            e.Label = e.Label.Skip(remainder.Count).ToList();
            newNode.AddEdge(e.Label[0], e);
            s.AddEdge(t, newEdge);
            return new Tuple<bool, Node<int>>(false, s);
            // they are different words. No prefix. but they may still share some common substr
        }

        static bool StartsWith(List<int> src, List<int> inner) {
            for(int i = 0; i<inner.Count; ++i) {
                if (i >= src.Count) {
                    return false;
                }
                if (src[i] != inner[i]) return false;
            }
            return true;
        }
        /**
         * Return a (NodeA<int>, string) (n, remainder) Tuple such that n is a farthest descendant of
         * s (the input NodeA<int>) that can be reached by following a path of edges denoting
         * a prefix of inputstr and remainder will be string that must be
         * appended to the concatenation of labels from s to n to get inpustr.
         */
        private static Tuple<Node<int>, List<int>> Canonize(Node<int> s, List<int> inputstr)
        {

            if (inputstr.Count == 0)
            {
                return new Tuple<Node<int>, List<int> >(s, inputstr);
            }

            var currentNode = s;
            var str = inputstr;
            var g = s.GetEdge(str[0]);
            // descend the tree as long as a proper label is found
            while (g != null && StartsWith(str, g.Label))
            {
                str = str.Skip(g.Label.Count).ToList();//  str.Substring(g.Label.Count);
                currentNode = g.Target;
                if (str.Count > 0)
                {
                    g = currentNode.GetEdge(str[0]);
                }
            }

            return new Tuple<Node<int>, List<int>>(currentNode, str);
        }

        /**
         * Updates the tree starting from inputNode and by adding stringPart.
         * 
         * Returns a reference (NodeA<int>, string) Tuple for the string that has been added so far.
         * This means:
         * - the NodeA<int> will be the NodeA<int> that can be reached by the longest path string (S1)
         *   that can be obtained by concatenating consecutive edges in the tree and
         *   that is a substring of the string added so far to the tree.
         * - the string will be the remainder that must be added to S1 to get the string
         *   added so far.
         * 
         * @param inputNode the NodeA<int> to start from
         * @param stringPart the string to add to the tree
         * @param rest the rest of the string
         * @param value the value to add to the index
         */
        private Tuple<Node<int>, List<int>> Update(Node<int> inputNode, List<int> stringPart, List<int> rest, int value)
        {
            var s = inputNode;
            var tempstr = stringPart;
            var newChar = stringPart[stringPart.Count - 1];
            // Console.WriteLine("stringPart: " + stringPart.Count + ", Rest count = " + rest.Count);
            // line 1
            var oldroot = _root;

            // line 1b
            var ret = TestAndSplit(s, tempstr.Take(tempstr.Count - 1).ToList(), newChar, rest, value);
            
            var r = ret.Item2;
            var endpoint = ret.Item1;
            
            // line 2
            while (!endpoint)
            {
                // line 3
                var tempEdge = r.GetEdge(newChar);
                Node<int> leaf;
                if (null != tempEdge)
                {
                    // such a NodeA<int> is already present. This is one of the main differences from Ukkonen's case:
                    // the tree can contain deeper nodes at this stage because different strings were added by previous iterations.
                    leaf = tempEdge.Target;
                }
                else
                {
                    // must build a new leaf
                    leaf = new Node<int>();
                    leaf.AddRef(value);
                    var newedge = new Edge<int>(rest, leaf);
                    r.AddEdge(newChar, newedge);
                }

                // update suffix link for newly created leaf
                if (_activeLeaf != _root)
                {
                    _activeLeaf.Suffix = leaf;
                }
                _activeLeaf = leaf;

                // line 4
                if (oldroot != _root)
                {
                    oldroot.Suffix = r;
                }

                // line 5
                oldroot = r;

                // line 6
                if (null == s.Suffix)
                {
                    // root NodeA<int>
                    //TODO Check why assert
                    //assert (root == s);
                    // this is a special case to handle what is referred to as NodeA<int> _|_ on the paper
                    tempstr = tempstr.Skip(1).ToList();
                }
                else
                {
                    var canret = Canonize(s.Suffix, SafeCutLastChar(tempstr));
                    s = canret.Item1;
                    // use intern to ensure that tempstr is a reference from the string pool
                    
                    var lastChar = tempstr[tempstr.Count - 1];
                    tempstr = new List<int>();
                    tempstr.AddRange(canret.Item2);
                    tempstr.Add(lastChar); // tempstr = canret.Item2 + tempstr[-1]
                }

                // line 7
                ret = TestAndSplit(s, SafeCutLastChar(tempstr), newChar, rest, value);
                r = ret.Item2;
                endpoint = ret.Item1;
            }

            // line 8
            if (oldroot != _root)
            {
                oldroot.Suffix = r;
            }

            return new Tuple<Node<int>, List<int>>(s, tempstr);
        }

        private static List<int> SafeCutLastChar(List<int> seq)
        {
            return seq.Count == 0 ? new List<int>() : seq.Take(seq.Count - 1).ToList();
        }


        byte[] toByte(int a) {
            return BitConverter.GetBytes(a);
        }
        public void Save(Stream stream){
            var queue = new Queue<Node<int>>();
            queue.Enqueue(_root);
            while(queue.Count > 0) {
                var front = queue.Dequeue();
                var data = front.data;
                var dataLen = front.data.Count;
                stream.Write(toByte(dataLen), 0, 4); // write 4 byte len
                foreach(var entry in data) {
                    stream.Write(toByte(entry), 0, 4); // write 4 byte len
                }
                var edgeCount = front.edges.Count;
                stream.Write(toByte(edgeCount), 0, 4); // write 4 byte len
                
                foreach(var entry in front.edges) {
                    // int label first
                    stream.Write(toByte(entry.Key), 0, 4);
                    // write label (sequence of int)
                    stream.Write(toByte(entry.Value.Label.Count), 0, 4);
                    foreach(var element in entry.Value.Label) {
                        stream.Write(toByte(element), 0, 4);
                    }
                    // child_count for next level interation
                    stream.Write(toByte(entry.Value.Target.edges.Count), 0, 4);

                    // push edge end-side node:
                    queue.Enqueue(entry.Value.Target);
                }
            }
            /*
            Scheme:
                dataLen [dataArray] edgeCount [label suffixes=(count [numbers]) child_count] // first level
                dataLen [dataArray] edgeCount [label suffixes=(count [numbers]) child_count] dataLen [dataArray] edgeCount [label suffixes=(count [numbers]) child_count] // second level
            */
        }

        // void load(Stream stream){

        // }
    }
}
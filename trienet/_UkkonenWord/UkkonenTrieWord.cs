using System;
using System.Collections.Generic;
using System.Linq;

/**
* vinhphuctadang
* vinhphuctadang@gmail.com
* Optimize for almost repeated suggestions
**/

namespace Gma.DataStructures.StringSearch.Word
{
    public class UkkonenTrieWord<T> : ITrie<T>
    {
        private readonly int _minSuffixLength;

        //The root of the suffix tree
        private readonly Node<T> _root;

        //The last leaf that was added during the update operation
        private Node<T> _activeLeaf;

        private Dictionary<string, int> _wordToIndex;

        public UkkonenTrieWord() : this(0, null)
        {
        }

        public UkkonenTrieWord(int minSuffixLength, Dictionary<string, int> wordToIndex) 
        {
            _minSuffixLength = minSuffixLength;
            _root = new Node<T>();
            _activeLeaf = _root;
            _wordToIndex = wordToIndex;
        }

        public IEnumerable<T> Retrieve(List<int> word)
        {
            if (word.Count < _minSuffixLength) return Enumerable.Empty<T>();
            var tmpNode = SearchNode(word);
            return tmpNode == null 
                ? Enumerable.Empty<T>() 
                : tmpNode.GetData();
        }

        public IEnumerable<T> Retrieve(string key)
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
         * Returns the tree NodeA<T> (if present) that corresponds to the given string.
         */
        private Node<T> SearchNode(List<int> word)
        {
            /*
             * Verifies if exists a path from the root to a NodeA<T> such that the concatenation
             * of all the labels on the path is a superstring of the given word.
             * If such a path is found, the last NodeA<T> on it is returned.
             */
            var currentNode = _root;

            for (var i = 0; i < word.Count; ++i)
            {
                var ch = word[i];
                // follow the EdgeA<T> corresponding to this char
                var currentEdge = currentNode.GetEdge(ch);
                if (null == currentEdge)
                {
                    // there is no EdgeA<T> starting with this char
                    return null;
                }
                var label = currentEdge.Label;
                var lenToMatch = Math.Min(word.Count - i, label.Count);

                if (!RegionMatches(word, i, label, 0, lenToMatch))
                {
                    // the label on the EdgeA<T> does not correspond to the one in the string to search
                    return null;
                }

                if (label.Count >= word.Count - i)
                {
                    return currentEdge.Target;
                }
                // advance to next NodeA<T>
                currentNode = currentEdge.Target;
                i += lenToMatch - 1;
            }

            return null;
        }


        /*  
            DEPRECATED
        */
        public void Add(string key, T value)
        {
            var words = key.Split(' ');
            var newKey = new List<int>();
            for(int i = 0; i<words.Length; ++i) newKey.Add(_wordToIndex[words[i]]);
            this.Add(newKey, value);
            // Console.WriteLine("-------- add dones");
        }

        public void Add(List<int> key, T value) {
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
         * and there is an EdgeA<T> g such that
         *     g.label = stringPart + rest
         * 
         * Then g will be split in two different edges, one having $end as label, and the other one
         * having rest as label.
         *
         * @param inputs the starting NodeA<T>
         * @param stringPart the string to search
         * @param t the following character
         * @param remainder the remainder of the string to add to the index
         * @param value the value to add to the index
         * @return a Tuple containing
         *                  true/false depending on whether (stringPart + t) is contained in the subtree starting in inputs
         *                  the last NodeA<T> that can be reached by following the path denoted by stringPart starting from inputs
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
        private static Tuple<bool, Node<T>> TestAndSplit(Node<T> inputs, List<int> stringPart, int t, List<int> remainder, T value)
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
                // must see whether "str" is substring of the label of an EdgeA<T>
                if (label.Count > str.Count && label[str.Count] == t)
                {
                    return new Tuple<bool, Node<T>>(true, s);
                }
                // need to split the EdgeA<T>
                var newlabel = label.Skip(str.Count).ToList();
                //assert (label.startsWith(str));

                // build a new NodeA<T>
                var r = new Node<T>();
                // build a new EdgeA<T>
                var newedge = new Edge<T>(str, r);

                g.Label = newlabel;

                // link s -> r
                r.AddEdge(newlabel[0], g);
                s.AddEdge(str[0], newedge);

                return new Tuple<bool, Node<T>>(false, r);
            }
            var e = s.GetEdge(t);
            if (null == e)
            {
                // if there is no t-transtion from s
                return new Tuple<bool, Node<T>>(false, s);
            }
            // if (remainder.Equals(e.Label))
            if (Equals(remainder, e.Label))
            {
                // update payload of destination NodeA<T>
                e.Target.AddRef(value);
                return new Tuple<bool, Node<T>>(true, s);
            }
            // if (remainder.StartsWith(e.Label))
            if (StartsWith(remainder, e.Label))
            {
                return new Tuple<bool, Node<T>>(true, s);
            }
            if (!StartsWith(e.Label, remainder))
            {
                return new Tuple<bool, Node<T>>(true, s);
            }
            // need to split as above
            var newNode = new Node<T>();
            newNode.AddRef(value);

            var newEdge = new Edge<T>(remainder, newNode);
            e.Label = e.Label.Skip(remainder.Count).ToList();
            newNode.AddEdge(e.Label[0], e);
            s.AddEdge(t, newEdge);
            return new Tuple<bool, Node<T>>(false, s);
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
         * Return a (NodeA<T>, string) (n, remainder) Tuple such that n is a farthest descendant of
         * s (the input NodeA<T>) that can be reached by following a path of edges denoting
         * a prefix of inputstr and remainder will be string that must be
         * appended to the concatenation of labels from s to n to get inpustr.
         */
        private static Tuple<Node<T>, List<int>> Canonize(Node<T> s, List<int> inputstr)
        {

            if (inputstr.Count == 0)
            {
                return new Tuple<Node<T>, List<int> >(s, inputstr);
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

            return new Tuple<Node<T>, List<int>>(currentNode, str);
        }

        /**
         * Updates the tree starting from inputNode and by adding stringPart.
         * 
         * Returns a reference (NodeA<T>, string) Tuple for the string that has been added so far.
         * This means:
         * - the NodeA<T> will be the NodeA<T> that can be reached by the longest path string (S1)
         *   that can be obtained by concatenating consecutive edges in the tree and
         *   that is a substring of the string added so far to the tree.
         * - the string will be the remainder that must be added to S1 to get the string
         *   added so far.
         * 
         * @param inputNode the NodeA<T> to start from
         * @param stringPart the string to add to the tree
         * @param rest the rest of the string
         * @param value the value to add to the index
         */
        private Tuple<Node<T>, List<int>> Update(Node<T> inputNode, List<int> stringPart, List<int> rest, T value)
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
                Node<T> leaf;
                if (null != tempEdge)
                {
                    // such a NodeA<T> is already present. This is one of the main differences from Ukkonen's case:
                    // the tree can contain deeper nodes at this stage because different strings were added by previous iterations.
                    leaf = tempEdge.Target;
                }
                else
                {
                    // must build a new leaf
                    leaf = new Node<T>();
                    leaf.AddRef(value);
                    var newedge = new Edge<T>(rest, leaf);
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
                    // root NodeA<T>
                    //TODO Check why assert
                    //assert (root == s);
                    // this is a special case to handle what is referred to as NodeA<T> _|_ on the paper
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

            return new Tuple<Node<T>, List<int>>(s, tempstr);
        }

        private static List<int> SafeCutLastChar(List<int> seq)
        {
            return seq.Count == 0 ? new List<int>() : seq.Take(seq.Count - 1).ToList();
        }
    }
}
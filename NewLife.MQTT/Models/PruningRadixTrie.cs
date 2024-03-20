using System.Diagnostics;

namespace NewLife.MQTT.Models;

public class RadixTrieNode
{
    public List<(String key, RadixTrieNode node)> Children;

    //Does this node represent the last character in a word? 
    //0: no word; >0: is word (termFrequencyCount)
    public Int64 termFrequencyCount;
    public Int64 termFrequencyCountChildMax;

    public RadixTrieNode(Int64 termfrequencyCount) => termFrequencyCount = termfrequencyCount;
}

/// <summary>基数字典树</summary>
public class RadixTrie
{
    private Int64 _termCount = 0;
    private Int64 _termCountLoaded = 0;
    private readonly RadixTrieNode _trie;

    public RadixTrie() => _trie = new RadixTrieNode(0);

    /// <summary>插入单词</summary>
    /// <param name="term"></param>
    /// <param name="termFrequencyCount"></param>
    public void AddTerm(String term, Int64 termFrequencyCount)
    {
        List<RadixTrieNode> nodeList = [];
        AddTerm(_trie, term, termFrequencyCount, 0, 0, nodeList);
    }

    public void UpdateMaxCounts(List<RadixTrieNode> nodeList, Int64 termFrequencyCount)
    {
        foreach (var node in nodeList)
        {
            if (termFrequencyCount > node.termFrequencyCountChildMax)
            {
                node.termFrequencyCountChildMax = termFrequencyCount;
            }
        }
    }

    public void AddTerm(RadixTrieNode curr, String term, Int64 termFrequencyCount, Int32 id, Int32 level, List<RadixTrieNode> nodeList)
    {
        try
        {
            nodeList.Add(curr);

            //test for common prefix (with possibly different suffix)
            var common = 0;
            if (curr.Children != null)
            {
                for (var j = 0; j < curr.Children.Count; j++)
                {
                    (var key, var node) = curr.Children[j];

                    for (var i = 0; i < Math.Min(term.Length, key.Length); i++) if (term[i] == key[i]) common = i + 1; else break;

                    if (common > 0)
                    {
                        //term already existed
                        //existing ab
                        //new      ab
                        if ((common == term.Length) && (common == key.Length))
                        {
                            if (node.termFrequencyCount == 0) _termCount++;
                            node.termFrequencyCount += termFrequencyCount;
                            UpdateMaxCounts(nodeList, node.termFrequencyCount);
                        }
                        //new is subkey
                        //existing abcd
                        //new      ab
                        //if new is shorter (== common), then node(count) and only 1. children add (clause2)
                        else if (common == term.Length)
                        {
                            //insert second part of oldKey as child 
                            var child = new RadixTrieNode(termFrequencyCount)
                            {
                                Children = new List<(String, RadixTrieNode)>
                            {
                               (key.Substring(common), node)
                            },
                                termFrequencyCountChildMax = Math.Max(node.termFrequencyCountChildMax, node.termFrequencyCount)
                            };
                            UpdateMaxCounts(nodeList, termFrequencyCount);

                            //insert first part as key, overwrite old node
                            curr.Children[j] = (term.Substring(0, common), child);
                            //sort children descending by termFrequencyCountChildMax to start lookup with most promising branch
                            curr.Children.Sort((x, y) => y.Item2.termFrequencyCountChildMax.CompareTo(x.Item2.termFrequencyCountChildMax));
                            //increment termcount by 1
                            _termCount++;
                        }
                        //if oldkey shorter (==common), then recursive addTerm (clause1)
                        //existing: te
                        //new:      test
                        else if (common == key.Length)
                        {
                            AddTerm(node, term.Substring(common), termFrequencyCount, id, level + 1, nodeList);
                        }
                        //old and new have common substrings
                        //existing: test
                        //new:      team
                        else
                        {
                            //insert second part of oldKey and of s as child 
                            var child = new RadixTrieNode(0)
                            {
                                Children = new List<(String, RadixTrieNode)>
                            {
                                (key.Substring(common), node) ,
                                 (term.Substring(common), new RadixTrieNode(termFrequencyCount))
                            },
                                termFrequencyCountChildMax = Math.Max(node.termFrequencyCountChildMax, Math.Max(termFrequencyCount, node.termFrequencyCount))
                            };//count       
                            UpdateMaxCounts(nodeList, termFrequencyCount);

                            //insert first part as key. overwrite old node
                            curr.Children[j] = (term.Substring(0, common), child);
                            //sort children descending by termFrequencyCountChildMax to start lookup with most promising branch
                            curr.Children.Sort((x, y) => y.Item2.termFrequencyCountChildMax.CompareTo(x.Item2.termFrequencyCountChildMax));
                            //increment termcount by 1 
                            _termCount++;
                        }
                        return;
                    }
                }
            }

            // initialize dictionary if first key is inserted 
            if (curr.Children == null)
            {
                curr.Children = new List<(String, RadixTrieNode)> { (term, new RadixTrieNode(termFrequencyCount)) };
            }
            else
            {
                curr.Children.Add((term, new RadixTrieNode(termFrequencyCount)));
                //sort children descending by termFrequencyCountChildMax to start lookup with most promising branch
                curr.Children.Sort((x, y) => y.Item2.termFrequencyCountChildMax.CompareTo(x.Item2.termFrequencyCountChildMax));
            }
            _termCount++;
            UpdateMaxCounts(nodeList, termFrequencyCount);
        }
        catch (Exception e) { Console.WriteLine("exception: " + term + " " + e.Message); }
    }

    public void FindAllChildTerms(String prefix, Int32 topK, ref Int64 termFrequencyCountPrefix, String prefixString, List<(String term, Int64 termFrequencyCount)> results, Boolean pruning) => FindAllChildTerms(prefix, _trie, topK, ref termFrequencyCountPrefix, prefixString, results, null, pruning);

    public void FindAllChildTerms(String prefix, RadixTrieNode curr, Int32 topK, ref Int64 termfrequencyCountPrefix, String prefixString, List<(String term, Int64 termFrequencyCount)> results, StreamWriter file, Boolean pruning)
    {
        try
        {
            //pruning/early termination in radix trie lookup
            if (pruning && (topK > 0) && (results.Count == topK) && (curr.termFrequencyCountChildMax <= results[topK - 1].termFrequencyCount)) return;

            //test for common prefix (with possibly different suffix)
            var noPrefix = String.IsNullOrEmpty(prefix);

            if (curr.Children != null)
            {
                foreach ((var key, var node) in curr.Children)
                {
                    //pruning/early termination in radix trie lookup
                    if (pruning && (topK > 0) && (results.Count == topK) && (node.termFrequencyCount <= results[topK - 1].termFrequencyCount) && (node.termFrequencyCountChildMax <= results[topK - 1].termFrequencyCount))
                    {
                        if (!noPrefix)
                            break;
                        else
                            continue;
                    }

                    if (noPrefix || key.StartsWith(prefix))
                    {
                        if (node.termFrequencyCount > 0)
                        {
                            if (prefix == key) termfrequencyCountPrefix = node.termFrequencyCount;

                            //candidate                              
                            if (file != null)
                                file.WriteLine(prefixString + key + "\t" + node.termFrequencyCount.ToString());
                            else if (topK > 0)
                                AddTopKSuggestion(prefixString + key, node.termFrequencyCount, topK, ref results);
                            else
                                results.Add((prefixString + key, node.termFrequencyCount));
                        }

                        if ((node.Children != null) && (node.Children.Count > 0))
                            FindAllChildTerms("", node, topK, ref termfrequencyCountPrefix, prefixString + key, results, file, pruning);
                        if (!noPrefix) break;
                    }
                    else if (prefix.StartsWith(key))
                    {

                        if ((node.Children != null) && (node.Children.Count > 0))
                            FindAllChildTerms(prefix.Substring(key.Length), node, topK, ref termfrequencyCountPrefix, prefixString + key, results, file, pruning);
                        break;
                    }
                }
            }
        }
        catch (Exception e) { Console.WriteLine("exception: " + prefix + " " + e.Message); }
    }

    public List<(String term, Int64 termFrequencyCount)> GetTopkTermsForPrefix(String prefix, Int32 topK, out Int64 termFrequencyCountPrefix, Boolean pruning = true)
    {
        List<(String term, Int64 termFrequencyCount)> results = [];

        //termFrequency of prefix, if it exists in the dictionary (even if not returned in the topK results due to low termFrequency)
        termFrequencyCountPrefix = 0;

        // At the end of the prefix, find all child words
        FindAllChildTerms(prefix, topK, ref termFrequencyCountPrefix, "", results, pruning);

        return results;
    }

    public void WriteTermsToFile(String path)
    {
        //save only if new terms were added
        if (_termCountLoaded == _termCount) return;

        try
        {
            using (var file = new StreamWriter(path))
            {
                Int64 prefixCount = 0;
                FindAllChildTerms("", _trie, 0, ref prefixCount, "", null, file, true);
            }
            Console.WriteLine(_termCount.ToString("N0") + " terms written.");
        }
        catch (Exception e)
        {
            Console.WriteLine("Writing terms exception: " + e.Message);
        }
    }

    public Boolean ReadTermsFromFile(String path)
    {
        if (!System.IO.File.Exists(path))
        {
            Console.WriteLine("Could not find file " + path);
            return false;
        }
        try
        {
            var sw1 = Stopwatch.StartNew();
            using (Stream corpusStream = System.IO.File.OpenRead(path))
            {
                using var sr = new StreamReader(corpusStream, System.Text.Encoding.UTF8, false);
                String line;

                //process a single line at a time only for memory efficiency
                while ((line = sr.ReadLine()) != null)
                {
                    var lineParts = line.Split('\t');
                    if (lineParts.Length == 2)
                    {
                        if (Int64.TryParse(lineParts[1], out var count))
                        {
                            this.AddTerm(lineParts[0], count);
                        }
                    }
                }
            }
            _termCountLoaded = _termCount;
            Console.WriteLine(_termCount.ToString("N0") + " terms loaded in " + sw1.ElapsedMilliseconds.ToString("N0") + " ms");
        }
        catch (Exception e)
        {
            Console.WriteLine("Loading terms exception: " + e.Message);
        }

        return true;
    }

    public class BinarySearchComparer : IComparer<(String term, Int64 termFrequencyCount)>
    {
        public Int32 Compare((String term, Int64 termFrequencyCount) f1, (String term, Int64 termFrequencyCount) f2) => Comparer<Int64>.Default.Compare(f2.termFrequencyCount, f1.termFrequencyCount);//descending
    }

    public void AddTopKSuggestion(String term, Int64 termFrequencyCount, Int32 topK, ref List<(String term, Int64 termFrequencyCount)> results)
    {
        //at the end/highest index is the lowest value
        // >  : old take precedence for equal rank   
        // >= : new take precedence for equal rank 
        if ((results.Count < topK) || (termFrequencyCount >= results[topK - 1].termFrequencyCount))
        {
            var index = results.BinarySearch((term, termFrequencyCount), new BinarySearchComparer());
            if (index < 0)
                results.Insert(~index, (term, termFrequencyCount));
            else results.Insert(index, (term, termFrequencyCount));

            if (results.Count > topK) results.RemoveAt(topK);
        }
    }
}
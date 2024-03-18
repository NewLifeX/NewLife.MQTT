using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NewLife.MQTT.Models;

/// <summary>前缀树节点</summary>
public class TrieNode
{
    public Dictionary<Char, TrieNode> Children { get; set; } = [];

    public List<String> Subscribers { get; set; } = [];
}

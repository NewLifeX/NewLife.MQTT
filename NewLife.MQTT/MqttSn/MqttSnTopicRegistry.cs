using System.Collections.Concurrent;

namespace NewLife.MQTT.MqttSn;

/// <summary>MQTT-SN topic ID registry. Maps topic names to 2-byte topic IDs</summary>
public class MqttSnTopicRegistry
{
    private readonly ConcurrentDictionary<String, UInt16> _nameToId = new();
    private readonly ConcurrentDictionary<UInt16, String> _idToName = new();
    private Int32 _nextId = 2;

    /// <summary>Register or lookup topic ID</summary>
    public UInt16 Register(String topicName)
    {
        if (_nameToId.TryGetValue(topicName, out var id)) return id;
        var newId = (UInt16)Interlocked.Increment(ref _nextId);
        if (newId < 2) newId = 2;
        _nameToId[topicName] = newId;
        _idToName[newId] = topicName;
        return newId;
    }

    /// <summary>Lookup topic name by ID</summary>
    public String? Lookup(UInt16 topicId)
    {
        _idToName.TryGetValue(topicId, out var name);
        return name;
    }

    /// <summary>Get topic ID by name</summary>
    public UInt16 GetId(String topicName)
    {
        _nameToId.TryGetValue(topicName, out var id);
        return id;
    }

    /// <summary>Remove topic mapping</summary>
    public void Remove(UInt16 topicId)
    {
        if (_idToName.TryRemove(topicId, out var name))
            _nameToId.TryRemove(name, out _);
    }

    /// <summary>Clear all registrations</summary>
    public void Clear()
    {
        _nameToId.Clear();
        _idToName.Clear();
        _nextId = 2;
    }
}

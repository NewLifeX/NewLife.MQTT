using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using NewLife.MQTT.Quic;
using Xunit;

namespace XUnitTestClient;

/// <summary>MqttUdpTransport 可靠 UDP 传输测试</summary>
public class MqttUdpTransportTests
{
    [Fact]
    public void ClientIsSupportedAlways()
    {
        Assert.True(MqttUdpClient.IsSupported);
    }

    [Fact]
    public void ListenerIsSupportedAlways()
    {
        Assert.True(MqttUdpListener.IsSupported);
    }

    [Fact]
    public async Task ConnectWithoutServerThrows()
    {
        var client = new MqttUdpClient();

        await Assert.ThrowsAsync<ArgumentNullException>(() => client.ConnectAsync());
    }

    [Fact]
    public void SendWithoutConnectionThrows()
    {
        var client = new MqttUdpClient { Server = "127.0.0.1" };

        Assert.Throws<InvalidOperationException>(() =>
        {
            client.SendAsync(new NewLife.MQTT.Messaging.PingRequest()).GetAwaiter().GetResult();
        });
    }

    [Fact]
    public void PeerSeqIdIncrements()
    {
        var peer = new UdpMqttPeer();

        var id1 = peer.NextSeqId();
        var id2 = peer.NextSeqId();
        var id3 = peer.NextSeqId();

        Assert.Equal((UInt16)1, id1);
        Assert.Equal((UInt16)2, id2);
        Assert.Equal((UInt16)3, id3);
    }

    [Fact]
    public void PeerSeqIdIsThreadSafe()
    {
        var peer = new UdpMqttPeer();
        var ids = new ConcurrentBag<UInt16>();

        Parallel.For(0, 1000, _ =>
        {
            ids.Add(peer.NextSeqId());
        });

        // 应有 1000 个不同的 ID
        Assert.Equal(1000, ids.Distinct().Count());
    }
}

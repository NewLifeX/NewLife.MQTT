using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using MQTTnet.Server;
using Xunit;

namespace XUnitTest.Integration;

/// <summary>MQTTnet 服务端集成测试固件。配合 IClassFixture&lt;CrossIntegrationFixture&gt; 使用，每个测试类只启动一次 MQTTnet 服务端</summary>
public sealed class CrossIntegrationFixture : IAsyncLifetime
{
    /// <summary>MQTTnet 服务端实例</summary>
    public MqttServer MqttnetServer { get; private set; } = null!;

    /// <summary>服务端实际监听端口</summary>
    public Int32 Port { get; private set; }

    public async Task InitializeAsync()
    {
        Port = GetFreePort();
        var factory = new MqttServerFactory();
        var options = factory.CreateServerOptionsBuilder()
            .WithDefaultEndpoint()
            .WithDefaultEndpointPort(Port)
            .Build();
        MqttnetServer = factory.CreateMqttServer(options);
        await MqttnetServer.StartAsync();
    }

    public async Task DisposeAsync()
    {
        await MqttnetServer.StopAsync();
        MqttnetServer.Dispose();
    }

    /// <summary>获取一个可用的随机端口</summary>
    internal static Int32 GetFreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}

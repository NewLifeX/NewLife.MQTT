using System;
using NewLife;
using NewLife.Log;
using NewLife.Model;
using NewLife.MQTT;

namespace XUnitTestClient;

/// <summary>集成测试共享服务端固件。配合 IClassFixture&lt;MqttServerFixture&gt; 使用，每个测试类只启动一次 MQTT 服务端</summary>
public sealed class MqttServerFixture : IDisposable
{
    /// <summary>MQTT 服务端实例</summary>
    public MqttServer Server { get; }

    /// <summary>服务端实际监听端口（OS 自动分配）</summary>
    public Int32 Port { get; }

    public MqttServerFixture()
    {
        Server = new MqttServer
        {
            Port = 0,
            ServiceProvider = ObjectContainer.Current.BuildServiceProvider(),
            Log = XTrace.Log,
            SessionLog = XTrace.Log,
        };
        Server.Start();
        Port = Server.Port;
    }

    public void Dispose() => Server.TryDispose();
}

using System.Net;
using System.Net.Sockets;
using System.Text;
using NewLife.Log;
using NewLife.Net;
using NewLife.Threading;

namespace NewLife.MQTT.Clusters;

/// <summary>集群节点自动发现。使用 UDP 广播侦测同网段内的其他集群节点</summary>
/// <remarks>
/// 工作原理：
/// 1. 每隔 <see cref="AnnounceInterval"/> 秒向广播地址发送节点信息（本机 IP:ClusterPort）。
/// 2. 同时监听广播端口，收到其他节点公告后自动调用 <see cref="ClusterServer.AddNode"/>。
/// 本功能仅在同一局域网内有效；公网环境请使用 <see cref="ClusterServer.ClusterNodes"/> 手动配置。
/// </remarks>
public class ClusterDiscovery : DisposeBase, ILogFeature
{
    #region 属性
    /// <summary>集群服务器引用</summary>
    public ClusterServer Cluster { get; set; } = null!;

    /// <summary>广播端口。默认为 ClusterPort + 1</summary>
    public Int32 DiscoveryPort { get; set; }

    /// <summary>节点公告间隔（秒）。默认 30</summary>
    public Int32 AnnounceInterval { get; set; } = 30;

    /// <summary>日志</summary>
    public ILog Log { get; set; } = Logger.Null;

    private UdpClient? _udpClient;
    private TimerX? _announceTimer;
    private CancellationTokenSource? _cts;
    #endregion

    #region 启动停止
    /// <summary>启动自动发现</summary>
    public void Start()
    {
        if (DiscoveryPort == 0) DiscoveryPort = Cluster.Port + 1;

        try
        {
            var udp = new UdpClient(DiscoveryPort) { EnableBroadcast = true };
            _udpClient = udp;

            _cts = new CancellationTokenSource();
            _ = ListenAsync(udp, _cts.Token);

            _announceTimer = new TimerX(DoAnnounce, null, 1_000, AnnounceInterval * 1000);

            WriteLog("集群自动发现已启动，广播端口 {0}", DiscoveryPort);
        }
        catch (Exception ex)
        {
            WriteLog("集群自动发现启动失败，端口 {0} 可能已被占用: {1}", DiscoveryPort, ex.Message);
        }
    }

    /// <summary>停止自动发现</summary>
    public void Stop()
    {
        _announceTimer.TryDispose();
        _announceTimer = null;

        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;

        _udpClient?.Close();
        _udpClient = null;
    }

    /// <summary>销毁</summary>
    protected override void Dispose(Boolean disposing)
    {
        base.Dispose(disposing);
        Stop();
    }
    #endregion

    #region 公告与监听
    private void DoAnnounce(Object state)
    {
        if (_udpClient == null) return;

        try
        {
            var myIp = NetHelper.MyIP()?.ToString() ?? "127.0.0.1";
            var payload = Encoding.UTF8.GetBytes($"MQTT-CLUSTER:{myIp}:{Cluster.Port}");
            var broadcastEp = new IPEndPoint(IPAddress.Broadcast, DiscoveryPort);
            _udpClient.Send(payload, payload.Length, broadcastEp);
        }
        catch { /* 广播失败不影响主流程 */ }
    }

    private async Task ListenAsync(UdpClient udp, CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
#if NET5_0_OR_GREATER
                var result = await udp.ReceiveAsync(token).ConfigureAwait(false);
#else
                UdpReceiveResult result;
                var receiveTask = udp.ReceiveAsync();
                var tcs = new TaskCompletionSource<Boolean>();
                using (token.Register(() => tcs.TrySetCanceled()))
                {
                    var completedTask = await Task.WhenAny(receiveTask, tcs.Task).ConfigureAwait(false);
                    if (completedTask == tcs.Task)
                    {
                        // 取消请求
                        break;
                    }
                    result = await receiveTask.ConfigureAwait(false);
                }
#endif
                var msg = Encoding.UTF8.GetString(result.Buffer);

                if (!msg.StartsWith("MQTT-CLUSTER:")) continue;

                // 解析 "MQTT-CLUSTER:{ip}:{port}"
                var parts = msg[13..].Split(':');
                if (parts.Length != 2) continue;

                var ip = parts[0];
                if (!Int32.TryParse(parts[1], out var port)) continue;

                // 判断是否为自身（通过 IP 比较）
                var myIp = NetHelper.MyIP()?.ToString();
                if (ip == myIp && port == Cluster.Port) continue;

                var endpoint = $"{ip}:{port}";
                if (Cluster.Nodes.ContainsKey(endpoint)) continue;

                WriteLog("发现集群节点 {0}，正在加入...", endpoint);
                Cluster.AddNode(endpoint);
            }
            catch (OperationCanceledException) { break; }
            catch { /* 继续监听 */ }
        }
    }
    #endregion

    #region 日志
    /// <summary>写日志</summary>
    public void WriteLog(String format, params Object[] args) => Log?.Info($"[Discovery]{format}", args);
    #endregion
}

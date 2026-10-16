using System.Diagnostics;
using NewLife;
using NewLife.Data;
using NewLife.Log;
using NewLife.Model;
using NewLife.MQTT;
using NewLife.MQTT.Messaging;
using NewLife.Net;

// 独立进程压测工具：Broker 与负载生成器分开进程运行，模拟竞品基准口径
// 用法（在仓库根目录）:
//   dotnet run -c Release --project Benchmark/LoadBench -- server <port> <seconds>
//   dotnet run -c Release --project Benchmark/LoadBench -- pub <port> <connections> <seconds>
//   dotnet run -c Release --project Benchmark/LoadBench -- sub <port> <connections> <seconds>
//   dotnet run -c Release --project Benchmark/LoadBench -- mix <port> <pubConns> <subConns> <seconds>
//   dotnet run -c Release --project Benchmark/LoadBench -- pair <port> <pairs> <seconds>
//   dotnet run -c Release --project Benchmark/LoadBench -- rawserver <port> <seconds>
//   dotnet run -c Release --project Benchmark/LoadBench -- rawclient <port> <connections> <seconds>
// 场景说明见 Doc/Benchmark/MQTT性能测试.md §10

var mode = args.Length > 0 ? args[0] : "help";

switch (mode)
{
    case "server":
        await RunServer(Int32.Parse(args[1]), Int32.Parse(args[2]));
        break;
    case "pub":
        await RunPublish(Int32.Parse(args[1]), Int32.Parse(args[2]), Int32.Parse(args[3]), 0);
        break;
    case "sub":
        await RunSubscribe(Int32.Parse(args[1]), Int32.Parse(args[2]), Int32.Parse(args[3]));
        break;
    case "mix":
        await RunPublish(Int32.Parse(args[1]), Int32.Parse(args[2]), Int32.Parse(args[4]), Int32.Parse(args[3]));
        break;
    case "pair":
        await RunPairs(Int32.Parse(args[1]), Int32.Parse(args[2]), Int32.Parse(args[3]));
        break;
    case "rawserver":
        await RunRawServer(Int32.Parse(args[1]), Int32.Parse(args[2]));
        break;
    case "rawclient":
        await RunRawClient(Int32.Parse(args[1]), Int32.Parse(args[2]), Int32.Parse(args[3]));
        break;
    default:
        Console.WriteLine("usage: server|pub|sub|mix|rawserver|rawclient ...");
        break;
}

static async Task RunServer(Int32 port, Int32 seconds)
{
    var services = ObjectContainer.Current;
    services.AddSingleton<ILog>(XTrace.Log);

    var server = new MqttServer
    {
        Port = port,
        ServiceProvider = services.BuildServiceProvider(),
        Log = Logger.Null,
        SessionLog = Logger.Null,
    };
    server.Start();
    Console.WriteLine($"[server] started on {port}, running {seconds}s");

    var sw = Stopwatch.StartNew();
    var lastRecv = 0L;
    var lastSent = 0L;
    var lastMs = 0L;

    while (sw.Elapsed.TotalSeconds < seconds)
    {
        await Task.Delay(2000);

        var st = server.Exchange?.Stats;
        if (st == null) continue;

        var recv = st.MessagesReceived;
        var sent = st.MessagesSent;
        var now = sw.ElapsedMilliseconds;
        var span = (now - lastMs) / 1000.0;
        var recvRate = (recv - lastRecv) / span;
        var sentRate = (sent - lastSent) / span;
        lastRecv = recv;
        lastSent = sent;
        lastMs = now;

        Console.WriteLine($"[server] t={sw.Elapsed.TotalSeconds:F0}s clients={st.ConnectedClients} recv={recvRate:F0}/s sent={sentRate:F0}/s totalRecv={recv} totalSent={sent}");
    }

    server.TryDispose();
    Console.WriteLine("[server] stopped");
}

static MqttClient CreateClient(Int32 port, String clientId) => new()
{
    Server = $"tcp://127.0.0.1:{port}",
    ClientId = clientId,
    Timeout = 30000,
    Reconnect = false,
    Log = Logger.Null,
};

static async Task RunPublish(Int32 port, Int32 connections, Int32 seconds, Int32 subConnections)
{
    // 先连接订阅者（mix 模式）
    var subs = new MqttClient[subConnections];
    var subCounts = new Int64[subConnections];
    for (var i = 0; i < subConnections; i++)
    {
        var s = CreateClient(port, $"lb_sub_{i}");
        var idx = i;
        s.Received += (sender, e) => Interlocked.Increment(ref subCounts[idx]);
        await s.ConnectAsync();
        await s.SubscribeAsync("bench/#");
        subs[i] = s;
    }

    // 连接发布者
    var pubs = new MqttClient[connections];
    for (var i = 0; i < connections; i++)
    {
        var p = CreateClient(port, $"lb_pub_{i}");
        await p.ConnectAsync();
        pubs[i] = p;
    }

    Console.WriteLine($"[client] {connections} publishers + {subConnections} subscribers connected");

    var payloadBytes = new Byte[64];
    for (var i = 0; i < payloadBytes.Length; i++) payloadBytes[i] = (Byte)('a' + i % 26);

    // 每个发布连接独享一个消息对象（QoS0 复用安全），降低负载生成器自身开销
    var messages = new PublishMessage[connections];
    var totalSent = new Int64[connections];
    for (var i = 0; i < connections; i++)
    {
        messages[i] = new PublishMessage
        {
            Topic = $"bench/{i}",
            QoS = QualityOfService.AtMostOnce,
            Payload = new ArrayPacket(payloadBytes),
        };
    }

    // 预热 3 秒
    using var warmup = new CancellationTokenSource(TimeSpan.FromSeconds(3));
    var tasks = new Task[connections];
    for (var i = 0; i < connections; i++)
    {
        var idx = i;
        var pub = pubs[idx];
        var msg = messages[idx];
        tasks[idx] = Task.Run(async () =>
        {
            try
            {
                while (!warmup.IsCancellationRequested)
                {
                    await pub.PublishAsync(msg);
                }
            }
            catch (OperationCanceledException) { }
        });
    }
    await Task.WhenAll(tasks);

    // 正式测量窗口
    Console.WriteLine("[client] warmup done, measuring...");
    var sw = Stopwatch.StartNew();
    for (var i = 0; i < connections; i++) Interlocked.Exchange(ref totalSent[i], 0);

    using var measure = new CancellationTokenSource(TimeSpan.FromSeconds(seconds));
    for (var i = 0; i < connections; i++)
    {
        var idx = i;
        var pub = pubs[idx];
        var msg = messages[idx];
        tasks[idx] = Task.Run(async () =>
        {
            try
            {
                while (!measure.IsCancellationRequested)
                {
                    await pub.PublishAsync(msg);
                    Interlocked.Increment(ref totalSent[idx]);
                }
            }
            catch (OperationCanceledException) { }
        });
    }
    await Task.WhenAll(tasks);
    sw.Stop();

    var total = totalSent.Sum();
    var subTotal = subCounts.Sum();
    Console.WriteLine($"[client] published {total} in {sw.Elapsed.TotalSeconds:F1}s => {total / sw.Elapsed.TotalSeconds:F0} publish/s");
    Console.WriteLine($"[client] per-connection: {total / connections / sw.Elapsed.TotalSeconds:F0}/s avg, min={totalSent.Min() / sw.Elapsed.TotalSeconds:F0}/s max={totalSent.Max() / sw.Elapsed.TotalSeconds:F0}/s");
    if (subConnections > 0)
        Console.WriteLine($"[client] subscribers received {subTotal} => {subTotal / sw.Elapsed.TotalSeconds:F0}/s total, per-sub {subTotal / subConnections / sw.Elapsed.TotalSeconds:F0}/s");

    foreach (var p in pubs) p.TryDispose();
    foreach (var s in subs) s.TryDispose();
}

static async Task RunSubscribe(Int32 port, Int32 connections, Int32 seconds)
{
    var counts = new Int64[connections];
    var subs = new MqttClient[connections];
    for (var i = 0; i < connections; i++)
    {
        var s = CreateClient(port, $"lb_sub_{i}");
        var idx = i;
        s.Received += (sender, e) => Interlocked.Increment(ref counts[idx]);
        await s.ConnectAsync();
        await s.SubscribeAsync("bench/#");
        subs[i] = s;
    }

    Console.WriteLine($"[client] {connections} subscribers connected, receiving {seconds}s...");
    var sw = Stopwatch.StartNew();
    await Task.Delay(TimeSpan.FromSeconds(seconds));
    sw.Stop();

    var total = counts.Sum();
    Console.WriteLine($"[client] received {total} in {sw.Elapsed.TotalSeconds:F1}s => {total / sw.Elapsed.TotalSeconds:F0}/s");
    foreach (var s in subs) s.TryDispose();
}

static async Task RunPairs(Int32 port, Int32 pairs, Int32 seconds)
{
    // N 对发布/订阅：发布 i → 订阅 bench/{i}，每消息恰好投递一次，端到端多连接吞吐
    var subs = new MqttClient[pairs];
    var counts = new Int64[pairs];
    for (var i = 0; i < pairs; i++)
    {
        var s = CreateClient(port, $"lb_pair_sub_{i}");
        var idx = i;
        s.Received += (sender, e) => Interlocked.Increment(ref counts[idx]);
        await s.ConnectAsync();
        await s.SubscribeAsync($"bench/{i}");
        subs[i] = s;
    }

    var pubs = new MqttClient[pairs];
    for (var i = 0; i < pairs; i++)
    {
        var p = CreateClient(port, $"lb_pair_pub_{i}");
        await p.ConnectAsync();
        pubs[i] = p;
    }

    Console.WriteLine($"[client] {pairs} pub/sub pairs connected");

    var payloadBytes = new Byte[64];
    var messages = new PublishMessage[pairs];
    for (var i = 0; i < pairs; i++)
    {
        messages[i] = new PublishMessage
        {
            Topic = $"bench/{i}",
            QoS = QualityOfService.AtMostOnce,
            Payload = new ArrayPacket(payloadBytes),
        };
    }

    // 预热 3 秒
    using (var warmup = new CancellationTokenSource(TimeSpan.FromSeconds(3)))
    {
        var warmTasks = new Task[pairs];
        for (var i = 0; i < pairs; i++)
        {
            var idx = i;
            var pub = pubs[idx];
            var msg = messages[idx];
            warmTasks[idx] = Task.Run(async () =>
            {
                try
                {
                    while (!warmup.IsCancellationRequested) await pub.PublishAsync(msg);
                }
                catch (OperationCanceledException) { }
            });
        }
        await Task.WhenAll(warmTasks);
    }

    var sent = new Int64[pairs];
    for (var i = 0; i < pairs; i++) Interlocked.Exchange(ref counts[i], 0);

    Console.WriteLine("[client] warmup done, measuring...");
    var sw = Stopwatch.StartNew();
    using (var measure = new CancellationTokenSource(TimeSpan.FromSeconds(seconds)))
    {
        var tasks = new Task[pairs];
        for (var i = 0; i < pairs; i++)
        {
            var idx = i;
            var pub = pubs[idx];
            var msg = messages[idx];
            tasks[idx] = Task.Run(async () =>
            {
                try
                {
                    while (!measure.IsCancellationRequested)
                    {
                        await pub.PublishAsync(msg);
                        Interlocked.Increment(ref sent[idx]);
                    }
                }
                catch (OperationCanceledException) { }
            });
        }
        await Task.WhenAll(tasks);
    }
    sw.Stop();

    // 等待投递追平（最长10秒）
    var target = sent.Sum();
    var deadline = Environment.TickCount64 + 10000;
    while (counts.Sum() < target && Environment.TickCount64 < deadline) await Task.Delay(50);
    var wall = sw.Elapsed.TotalSeconds;

    var totalSent = sent.Sum();
    var totalRecv = counts.Sum();
    Console.WriteLine($"[client] sent {totalSent} in {wall:F1}s => {totalSent / wall:F0} publish/s");
    Console.WriteLine($"[client] received {totalRecv} => {totalRecv / wall:F0} delivered/s end-to-end, per-pair {totalRecv / pairs / wall:F0}/s");

    foreach (var p in pubs) p.TryDispose();
    foreach (var s in subs) s.TryDispose();
}

static async Task RunRawServer(Int32 port, Int32 seconds)
{
    var bytes = 0L;
    var reads = 0L;
    var server = new NetServer { Port = port, Log = Logger.Null };
    server.Received += (s, e) =>
    {
        Interlocked.Add(ref bytes, e.Packet?.Length ?? 0);
        Interlocked.Increment(ref reads);
    };
    server.Start();
    Console.WriteLine($"[rawserver] started on {port}, running {seconds}s");

    var sw = Stopwatch.StartNew();
    var last = 0L;
    while (sw.Elapsed.TotalSeconds < seconds)
    {
        await Task.Delay(2000);
        var now = Interlocked.Read(ref bytes);
        var r = Interlocked.Read(ref reads);
        var rate = (now - last) / 2.0;
        Console.WriteLine($"[rawserver] t={sw.Elapsed.TotalSeconds:F0}s {rate / 1048576.0:F1} MB/s, ~{rate / 64:F0} pkt(64B)/s, reads={r}");
        last = now;
    }

    server.TryDispose();
    Console.WriteLine("[rawserver] stopped");
}

static async Task RunRawClient(Int32 port, Int32 connections, Int32 seconds)
{
    var clients = new ISocketClient[connections];
    for (var i = 0; i < connections; i++)
    {
        var uri = new NetUri($"tcp://127.0.0.1:{port}") { Type = NetType.Tcp };
        var cli = uri.CreateRemote();
        cli.Log = Logger.Null;
        cli.Open();
        clients[i] = cli;
    }

    var payload = new ArrayPacket(new Byte[64]);

    // 预热
    using (var warmup = new CancellationTokenSource(TimeSpan.FromSeconds(2)))
    {
        var warmTasks = clients.Select(c => Task.Run(() =>
        {
            while (!warmup.IsCancellationRequested) c.Send(payload);
        })).ToArray();
        await Task.WhenAll(warmTasks);
    }

    var counts = new Int64[connections];
    var sw = Stopwatch.StartNew();
    using (var measure = new CancellationTokenSource(TimeSpan.FromSeconds(seconds)))
    {
        var tasks = new Task[connections];
        for (var i = 0; i < connections; i++)
        {
            var idx = i;
            var c = clients[idx];
            tasks[idx] = Task.Run(() =>
            {
                while (!measure.IsCancellationRequested)
                {
                    c.Send(payload);
                    counts[idx]++;
                }
            });
        }
        await Task.WhenAll(tasks);
    }
    sw.Stop();

    var total = counts.Sum();
    Console.WriteLine($"[rawclient] sent {total} in {sw.Elapsed.TotalSeconds:F1}s => {total / sw.Elapsed.TotalSeconds:F0} packet/s");

    foreach (var c in clients) c.TryDispose();
}

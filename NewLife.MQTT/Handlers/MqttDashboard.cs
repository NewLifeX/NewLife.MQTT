using System.Net;
using System.Net.Http;
using System.Text;
using NewLife.Log;
using NewLife.Model;

namespace NewLife.MQTT.Handlers;

/// <summary>MQTT Broker Web 可视化管理看板</summary>
/// <remarks>
/// 在独立端口上通过内置 HttpListener 提供单页 HTML 看板，自动轮询 <see cref="MqttManagementServer"/> 的 JSON API。
/// <para>默认端口 = ManagementPort + 1，例如管理 API 端口 18883，则看板端口默认为 18884。</para>
/// <para>启用：<c>server.ManagementPort = 18883; server.EnableDashboard = true;</c></para>
/// <para>访问：http://127.0.0.1:18884/</para>
/// </remarks>
public class MqttDashboard : DisposeBase, IServer, ILogFeature
{
    #region 属性
    /// <summary>服务名称</summary>
    public String Name { get; set; } = "MqttDashboard";

    /// <summary>看板监听端口。默认 0 = ManagementPort + 1</summary>
    public Int32 Port { get; set; }

    /// <summary>关联的管理服务器（用于获取 API 端口）</summary>
    public MqttManagementServer ManagementServer { get; set; } = null!;

    /// <summary>是否仅监听 localhost（默认 true）。改为 false 时需要管理员权限或 URL ACL 注册</summary>
    public Boolean LocalhostOnly { get; set; } = true;

    /// <summary>日志</summary>
    public ILog Log { get; set; } = Logger.Null;

    private HttpListener? _listener;
    private CancellationTokenSource? _cts;
    private HttpClient? _proxy;
    private Int32 _apiPort;
    #endregion

    #region 构造
    /// <summary>销毁</summary>
    protected override void Dispose(Boolean disposing)
    {
        base.Dispose(disposing);
        Stop(disposing ? "Dispose" : "GC");
    }
    #endregion

    #region 启动停止
    /// <summary>启动看板服务</summary>
    public void Start()
    {
        _apiPort = ManagementServer.Port;
        var dashPort = Port > 0 ? Port : _apiPort + 1;
        Port = dashPort;

        _proxy = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };

        var host = LocalhostOnly ? "localhost" : "+";
        var prefix = $"http://{host}:{dashPort}/";

        var listener = new HttpListener();
        listener.Prefixes.Add(prefix);

        try
        {
            listener.Start();
        }
        catch (HttpListenerException ex)
        {
            // 如非 localhost 模式启动失败，回退到 localhost
            if (!LocalhostOnly)
            {
                WriteLog("看板以 http://+:{0}/ 启动失败（{1}），回退到 localhost 模式", dashPort, ex.Message);
                listener = new HttpListener();
                listener.Prefixes.Add($"http://localhost:{dashPort}/");
                listener.Start();
            }
            else throw;
        }

        _listener = listener;
        _cts = new CancellationTokenSource();
        _ = ServeAsync(listener, _cts.Token);

        WriteLog("Dashboard 已启动，访问地址：http://localhost:{0}/", dashPort);
    }

    /// <summary>停止看板服务</summary>
    public void Stop(String? reason)
    {
        _cts?.Cancel();
        _cts = null;

        try { _listener?.Stop(); } catch { }
        _listener = null;

        _proxy?.Dispose();
        _proxy = null;
    }
    #endregion

    #region HTTP 服务循环
    private async Task ServeAsync(HttpListener listener, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            HttpListenerContext? ctx = null;
            try
            {
#if NET6_0_OR_GREATER
                ctx = await listener.GetContextAsync().WaitAsync(ct).ConfigureAwait(false);
#else
                var getContextTask = listener.GetContextAsync();
                var tcs = new TaskCompletionSource<Boolean>();
                using (ct.Register(() => tcs.TrySetCanceled()))
                {
                    var completedTask = await Task.WhenAny(getContextTask, tcs.Task).ConfigureAwait(false);
                    if (completedTask == tcs.Task)
                    {
                        // 取消请求
                        break;
                    }
                    ctx = await getContextTask.ConfigureAwait(false);
                }
#endif
            }
            catch (OperationCanceledException) { break; }
            catch { break; }

            _ = Task.Run(() => HandleAsync(ctx, _proxy!, _apiPort), ct);
        }
    }

    private static async Task HandleAsync(HttpListenerContext ctx, HttpClient proxy, Int32 apiPort)
    {
        try
        {
            var req = ctx.Request;
            var resp = ctx.Response;
            var path = req.Url?.AbsolutePath ?? "/";
            var query = req.Url?.Query ?? String.Empty;

            if (path == "/" || path == "/dashboard" || path == "/index.html")
            {
                // 服务 Dashboard HTML 页面（前端调 /api/* 均为同源代理，无 CORS 问题）
                var html = GetDashboardHtml();
                var data = Encoding.UTF8.GetBytes(html);
                resp.ContentType = "text/html; charset=utf-8";
                resp.ContentLength64 = data.Length;
#if NET6_0_OR_GREATER
                await resp.OutputStream.WriteAsync(data.AsMemory(), default).ConfigureAwait(false);
#else
                await resp.OutputStream.WriteAsync(data, 0, data.Length).ConfigureAwait(false);
#endif
            }
            else if (path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase))
            {
                // 代理转发到管理 JSON API：/api/stats → http://localhost:{apiPort}/Api/Stats
                var action = path.Substring("/api/".Length);
                var apiAction = Char.ToUpperInvariant(action[0]) + action.Substring(1);
                var apiUrl = $"http://localhost:{apiPort}/Api/{apiAction}{query}";

                try
                {
                    var apiResp = await proxy.GetAsync(apiUrl).ConfigureAwait(false);
#if NET5_0_OR_GREATER
                    var body = await apiResp.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
#else
                    var body = await apiResp.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
#endif
                    resp.ContentType = "application/json; charset=utf-8";
                    resp.ContentLength64 = body.Length;
#if NET6_0_OR_GREATER
                    await resp.OutputStream.WriteAsync(body.AsMemory(), default).ConfigureAwait(false);
#else
                    await resp.OutputStream.WriteAsync(body, 0, body.Length).ConfigureAwait(false);
#endif
                }
                catch
                {
                    var err = Encoding.UTF8.GetBytes("{\"code\":500,\"message\":\"API \u8bf7\u6c42\u5931\u8d25\"}");
                    resp.StatusCode = 500;
                    resp.ContentType = "application/json; charset=utf-8";
                    resp.ContentLength64 = err.Length;
#if NET6_0_OR_GREATER
                    await resp.OutputStream.WriteAsync(err.AsMemory(), default).ConfigureAwait(false);
#else
                    await resp.OutputStream.WriteAsync(err, 0, err.Length).ConfigureAwait(false);
#endif
                }
            }
            else
            {
                resp.StatusCode = 404;
            }

            resp.Close();
        }
        catch { try { ctx.Response.Close(); } catch { } }
    }
    #endregion

    #region Dashboard HTML
    private static String GetDashboardHtml() => @"<!DOCTYPE html>
<html lang=""zh-cn"">
<head>
<meta charset=""UTF-8"">
<meta name=""viewport"" content=""width=device-width,initial-scale=1"">
<title>MQTT Broker Dashboard</title>
<style>
*{box-sizing:border-box;margin:0;padding:0}
body{font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',Arial,sans-serif;background:#f0f2f5;color:#333}
header{background:#1677ff;color:#fff;padding:14px 24px;display:flex;justify-content:space-between;align-items:center}
header h1{font-size:19px;font-weight:600}
.hs{font-size:12px;opacity:.9}
.wrap{max-width:1200px;margin:20px auto;padding:0 16px}
.cards{display:grid;grid-template-columns:repeat(auto-fit,minmax(170px,1fr));gap:14px;margin-bottom:20px}
.card{background:#fff;border-radius:8px;padding:18px 20px;box-shadow:0 1px 3px rgba(0,0,0,.1)}
.card .lbl{font-size:12px;color:#888;margin-bottom:6px}
.card .val{font-size:26px;font-weight:700;color:#1677ff}
.card .sub{font-size:11px;color:#aaa;margin-top:3px}
.sec{background:#fff;border-radius:8px;padding:18px 20px;box-shadow:0 1px 3px rgba(0,0,0,.1);margin-bottom:14px}
.sec h2{font-size:15px;font-weight:600;color:#333;margin-bottom:12px}
.grid2{display:grid;grid-template-columns:1fr 1fr;gap:14px}
table{width:100%;border-collapse:collapse;font-size:13px}
th{background:#fafafa;font-weight:600;text-align:left;padding:7px 12px;border-bottom:2px solid #f0f0f0;color:#555}
td{padding:7px 12px;border-bottom:1px solid #f5f5f5}
tr:last-child td{border-bottom:none}
tr:hover td{background:#f8faff}
.bdg{display:inline-block;background:#e6f4ff;color:#1677ff;border-radius:10px;padding:1px 8px;font-size:12px}
.bar{display:flex;align-items:center;gap:6px;font-size:12px;color:#888;margin-bottom:16px}
.dot{width:8px;height:8px;border-radius:50%;background:#52c41a;display:inline-block}
.dot.off{background:#ff4d4f}
.empty{color:#bbb;text-align:center;padding:20px;font-size:13px}
.mt{color:#aaa;font-size:12px;text-align:right;margin-top:8px}
</style>
</head>
<body>
<header>
  <h1>&#x1F6F0; MQTT Broker Dashboard</h1>
  <div class=""hs"" id=""hs"">连接中…</div>
</header>
<div class=""wrap"">
  <div class=""bar""><span id=""dot"" class=""dot off""></span><span id=""rl"">未连接</span></div>
  <div class=""cards"">
    <div class=""card""><div class=""lbl"">在线客户端</div><div class=""val"" id=""c1"">—</div><div class=""sub"">峰值 <span id=""c1m"">—</span></div></div>
    <div class=""card""><div class=""lbl"">接收速率</div><div class=""val"" id=""c2"">—</div><div class=""sub"">条/秒</div></div>
    <div class=""card""><div class=""lbl"">发送速率</div><div class=""val"" id=""c3"">—</div><div class=""sub"">条/秒</div></div>
    <div class=""card""><div class=""lbl"">活跃订阅</div><div class=""val"" id=""c4"">—</div><div class=""sub"">主题数 <span id=""c4t"">—</span></div></div>
    <div class=""card""><div class=""lbl"">保留消息</div><div class=""val"" id=""c5"">—</div><div class=""sub"">条</div></div>
    <div class=""card""><div class=""lbl"">累计消息</div><div class=""val"" id=""c6"">—</div><div class=""sub"">收 <span id=""c6r"">—</span> 发 <span id=""c6s"">—</span></div></div>
  </div>
  <div class=""grid2"">
    <div class=""sec"">
      <h2>已连接客户端 <span class=""bdg"" id=""cCnt"">0</span></h2>
      <table><thead><tr><th>#</th><th>Client ID</th></tr></thead><tbody id=""cTb""></tbody></table>
    </div>
    <div class=""sec"">
      <h2>主题订阅 <span class=""bdg"" id=""tCnt"">0</span></h2>
      <table><thead><tr><th>主题</th><th style=""width:70px;text-align:right"">订阅数</th></tr></thead><tbody id=""tTb""></tbody></table>
    </div>
  </div>
  <div class=""sec"">
    <h2>保留消息主题 <span class=""bdg"" id=""rCnt"">0</span></h2>
    <table><thead><tr><th>#</th><th>主题</th></tr></thead><tbody id=""rTb""></tbody></table>
  </div>
  <p class=""mt"" id=""ut""></p>
</div>
<script>
var cd=5,cdT=null;
function fmt(n){if(n==null)return'—';if(n>=1e9)return(n/1e9).toFixed(1)+'B';if(n>=1e6)return(n/1e6).toFixed(1)+'M';if(n>=1e4)return(n/1e3).toFixed(0)+'K';return''+n;}
function tb(id,rows){document.getElementById(id).innerHTML=rows||'<tr><td colspan=""9"" class=""empty"">暂无数据</td></tr>';}
function esc(s){return(''+s).replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;');}
function g(o,a,b){return o[a]!==undefined?o[a]:(o[b]!==undefined?o[b]:0);}
async function load(){
  try{
    var[s,cl,tp,re]=await Promise.all([
      fetch('/api/stats').then(r=>r.json()),
      fetch('/api/clients').then(r=>r.json()),
      fetch('/api/topics').then(r=>r.json()),
      fetch('/api/retained').then(r=>r.json()),
    ]);
    var sd=s.data||s,cld=cl.data||cl,tpd=tp.data||tp,red=re.data||re;
    document.getElementById('c1').textContent=fmt(g(sd,'connectedClients','ConnectedClients'));
    document.getElementById('c1m').textContent=fmt(g(sd,'maxConnectedClients','MaxConnectedClients'));
    document.getElementById('c2').textContent=fmt(g(sd,'messagesReceivedPerSecond','MessagesReceivedPerSecond'));
    document.getElementById('c3').textContent=fmt(g(sd,'messagesSentPerSecond','MessagesSentPerSecond'));
    document.getElementById('c4').textContent=fmt(g(sd,'subscriptions','Subscriptions'));
    document.getElementById('c4t').textContent=fmt(g(sd,'topics','Topics'));
    document.getElementById('c5').textContent=fmt(g(sd,'retainedMessages','RetainedMessages'));
    var rx=g(sd,'messagesReceived','MessagesReceived'),tx=g(sd,'messagesSent','MessagesSent');
    document.getElementById('c6').textContent=fmt(rx+tx);
    document.getElementById('c6r').textContent=fmt(rx);
    document.getElementById('c6s').textContent=fmt(tx);
    var ut=g(sd,'uptime','Uptime'),st=g(sd,'startTime','StartTime');
    document.getElementById('ut').textContent='启动：'+(st||'')+'　运行：'+(ut||'');
    document.getElementById('cCnt').textContent=cld.length;
    tb('cTb',cld.length?cld.map(function(c,i){return'<tr><td>'+(i+1)+'</td><td>'+esc(c)+'</td></tr>';}).join(''):'');
    var tkeys=Object.keys(tpd||{});
    document.getElementById('tCnt').textContent=tkeys.length;
    tb('tTb',tkeys.length?tkeys.map(function(k){return'<tr><td>'+esc(k)+'</td><td style=""text-align:right""><span class=""bdg"">'+tpd[k]+'</span></td></tr>';}).join(''):'');
    document.getElementById('rCnt').textContent=red.length;
    tb('rTb',red.length?red.map(function(r,i){return'<tr><td>'+(i+1)+'</td><td>'+esc(r)+'</td></tr>';}).join(''):'');
    document.getElementById('dot').className='dot';
    document.getElementById('hs').textContent='运行中 · '+new Date().toLocaleTimeString();
    document.getElementById('rl').textContent='5秒后刷新';cd=5;
  }catch(e){
    document.getElementById('dot').className='dot off';
    document.getElementById('rl').textContent='连接失败：'+e.message;
    document.getElementById('hs').textContent='连接失败';
  }
}
window.onload=function(){
  load();
  cdT=setInterval(function(){cd--;if(cd<=0){cd=5;load();document.getElementById('rl').textContent='5秒后刷新';}else document.getElementById('rl').textContent=cd+'秒后刷新';},1000);
};
</script>
</body>
</html>";
    #endregion

    #region 日志
    /// <summary>输出日志</summary>
    public void WriteLog(String format, params Object[] args) => Log?.Info(format, args);
    #endregion
}

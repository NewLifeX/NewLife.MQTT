using System;
using NewLife.Agent;
using NewLife.Log;
using NewLife.MQTT;

namespace NewLife.MQTTServer
{
    class Program
    {
        static void Main(String[] args) => new MyService().Main();

        class MyService : AgentServiceBase<MyService>
        {
            public MyService()
            {
                ServiceName = "MqttServer";
                DisplayName = "MQTT服务器";
            }

            private MqttServer _Server;
            protected override void StartWork(String reason)
            {
                // 配置
                var set = Setting.Current;

                // 服务器
                var svr = new MqttServer()
                {
                    Port=set.Port,
                    Log = XTrace.Log,
                };

                if (set.Debug) svr.SessionLog = XTrace.Log;

                svr.Start();

                _Server = svr;

                base.StartWork(reason);
            }

            protected override void StopWork(String reason)
            {
                _Server.TryDispose();
                _Server = null;

                base.StopWork(reason);
            }
        }
    }
}
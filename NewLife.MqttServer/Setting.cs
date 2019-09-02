using System;
using System.ComponentModel;
using NewLife.Xml;

namespace NewLife.MQTTServer
{
    [XmlConfigFile(@"Config\MQTTServer.config", 10_000)]
    public class Setting : XmlConfig<Setting>
    {
        /// <summary>调试开关。默认 false</summary>
        [Description("调试开关。默认 false")]
        public Boolean Debug { get; set; }

        /// <summary>端口。默认 6002</summary>
        [Description("端口。默认 6002")]
        public Int32 Port { get; set; } = 6002;
    }
}
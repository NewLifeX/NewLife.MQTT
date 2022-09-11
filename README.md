# NewLife.MQTT - MQTT协议

![GitHub top language](https://img.shields.io/github/languages/top/newlifex/newlife.mqtt?logo=github)
![GitHub License](https://img.shields.io/github/license/newlifex/newlife.mqtt?logo=github)
![Nuget Downloads](https://img.shields.io/nuget/dt/newlife.mqtt?logo=nuget)
![Nuget](https://img.shields.io/nuget/v/newlife.mqtt?logo=nuget)
![Nuget (with prereleases)](https://img.shields.io/nuget/vpre/newlife.mqtt?label=dev%20nuget&logo=nuget)

MQTT协议是物联网领域最流行的通信协议！  
NewLife.MQTT包含了MQTT的完整实现，并实现了客户端MqttClient，以及服务端MqttServer。  
其中MqttServer仅实现基本网络框架，支持消息收发，完整的消息交换功能位于商用版IoT平台NewLife.IoT中。  

## MQTT协议
最流行的物联网通信协议MQTT，包括客户端、服务端和Web管理平台。  

提供订阅/发布模式，更为简约、轻量，易于使用，针对受限环境（带宽低、网络延迟高、网络通信不稳定），可以简单概括为物联网打造，官方总结特点如下：  
1.使用发布/订阅消息模式，提供一对多的消息发布，解除应用程序耦合。  
2.对负载内容屏蔽的消息传输。  
3.使用 TCP/IP 提供网络连接。  
4.有三种消息发布服务质量：  
“至多一次”，消息发布完全依赖底层 TCP/IP 网络。会发生消息丢失或重复。这一级别可用于如下情况，环境传感器数据，丢失一次读记录无所谓，因为不久后还会有第二次发送。  
“至少一次”，确保消息到达，但消息重复可能会发生。  
“只有一次”，确保消息到达一次。这一级别可用于如下情况，在计费系统中，消息重复或丢失会导致不正确的结果。  
5.小型传输，开销很小（固定长度的头部是 2 字节），协议交换最小化，以降低网络流量。  
6.使用 Last Will 和 Testament 特性通知有关各方客户端异常中断的机制。  

## MQTT 发布与订阅
发布时，指定消息Qos，broker保存的消息包含了Qos；  
订阅时，指定这次订阅要求的Qos，broker回复授权使用的Qos，一般就是申请那个；  
消费时，消息的Qos取发布订阅中较小者！  

详细场景：  
订阅Qos=0，不管发布什么消息，消费到的消息Qos都是0；  
订阅Qos=1，发布消息Qos=0时，消费得到Qos=0，发布消息Qos=1或2时，消费得到Qos=1；  
订阅Qos=2，消费得到的消息Qos，就是发布时的Qos；  
发布Qos=0，broker不做任何答复，理论上中途丢了都不知道，但是因为Tcp，如果网络异常客户端能发现；  
发布Qos=1，broker答复PubAck，表示已经收到消息；  
发布Qos=2，broker答复PubRec，客户端再次发送PubRel，broker答复PubComp，消息才算发布完成；
订阅Qos=2，broker推送Qos=2消息，客户端先回PubRec，broker再次发送PubRel，客户端答复PubComp，消息才算消费完成；  
发布Qos=2消息时，双重确认流程不需要等消费端在线，仅限于发布者与broker之间即可完成。  


## 新生命项目矩阵
各项目默认支持net6.0/netstandard2.1/netstandard2.0/net4.61，旧版（2021.1225）支持net4.5/net4.0/net2.0  

|                               项目                               | 年份  |  状态  |  .NET6  | 说明                                                                                 |
| :--------------------------------------------------------------: | :---: | :----: | :-----: | ------------------------------------------------------------------------------------ |
|                             基础组件                             |       |        |         | 支撑其它中间件以及产品项目                                                           |
|          [NewLife.Core](https://github.com/NewLifeX/X)           | 2002  | 维护中 |    √    | 日志、配置、缓存、网络、序列化、APM性能追踪                                     |
|    [NewLife.XCode](https://github.com/NewLifeX/NewLife.XCode)    | 2005  | 维护中 |    √    | 大数据中间件，MySQL/SQLite/SqlServer/Oracle/TDengine/达梦，自动分表              |
|      [NewLife.Net](https://github.com/NewLifeX/NewLife.Net)      | 2005  | 维护中 |    √    | 网络库，单机千万级吞吐率（2266万tps），单机百万级连接（400万Tcp）                    |
| [NewLife.Remoting](https://github.com/NewLifeX/NewLife.Remoting) | 2005  | 维护中 |    √    | RPC通信框架，内网高吞吐或物联网硬件设备场景                    |
|     [NewLife.Cube](https://github.com/NewLifeX/NewLife.Cube)     | 2010  | 维护中 |    √    | 魔方快速开发平台，集成了用户权限、SSO登录、OAuth服务端等，单表100亿级项目验证        |
|    [NewLife.Agent](https://github.com/NewLifeX/NewLife.Agent)    | 2008  | 维护中 |    √    | 服务管理组件，把应用安装成为操作系统守护进程，Windows服务、Linux的Systemd            |
|     [NewLife.Zero](https://github.com/NewLifeX/NewLife.Zero)     | 2020  | 维护中 |    √    | Zero零代脚手架，各种拷贝即用的项目模板，Web、WebApi、Service      |
|                              中间件                              |       |        |         | 对接知名中间件平台                                                                 |
|    [NewLife.Redis](https://github.com/NewLifeX/NewLife.Redis)    | 2017  | 维护中 |    √    | Redis客户端，微秒级延迟，百万级吞吐，丰富的消息队列，百亿级数据量项目验证            |
| [NewLife.RocketMQ](https://github.com/NewLifeX/NewLife.RocketMQ) | 2018  | 维护中 |    √    | 支持Apache RocketMQ和阿里云消息队列，十亿级项目验证                                  |
|     [NewLife.MQTT](https://github.com/NewLifeX/NewLife.MQTT)     | 2019  | 维护中 |    √    | 物联网消息协议，MqttClient/MqttServer，客户端支持阿里云物联网             |
|      [NewLife.IoT](https://github.com/NewLifeX/NewLife.IoT)      | 2022  | 维护中 |    √    | IoT标准库，定义物联网领域的各种通信协议标准规范                                               |
|   [NewLife.Modbus](https://github.com/NewLifeX/NewLife.Modbus)   | 2022  | 维护中 |    √    | 基于IoT标准库实现的ModbusTcp/ModbusRTU协议库，支持IoT平台和IoTEdge                            |
|     [NewLife.LoRa](https://github.com/NewLifeX/NewLife.LoRa)     | 2016  | 维护中 |    √    | 超低功耗的物联网远程通信协议LoRaWAN                                                  |
|                             产品平台                             |       |        |         | 产品平台级，编译部署即用，个性化自定义                                               |
|           [AntJob](https://github.com/NewLifeX/AntJob)           | 2019  | 维护中 |    √    | 蚂蚁调度，分布式大数据计算平台（实时/离线），蚂蚁搬家分片思想，万亿级数据量项目验证  |
|         [Stardust](https://github.com/NewLifeX/Stardust)         | 2018  | 维护中 |    √    | 星尘，分布式服务平台，节点管理、APM监控中心、配置中心、注册中心、发布中心、消息中心  |
|       [CrazyCoder](https://github.com/NewLifeX/XCoder)           | 2006  | 维护中 |    √    | 码神工具，众多开发者工具，网络、串口、加解密、正则表达式、Modbus                     |
|           [XProxy](https://github.com/NewLifeX/XProxy)           | 2005  | 维护中 |    √    | 产品级反向代理，NAT代理、Http代理                                                    |
|        [HttpMeter](https://github.com/NewLifeX/HttpMeter)        | 2022  | 维护中 |    √    | Http压力测试工具                                                    |
|          [SmartOS](https://github.com/NewLifeX/SmartOS)          | 2014  | 维护中 |  C++11  | 嵌入式操作系统，完全独立自主，支持ARM Cortex-M芯片架构                                   |
|         [GitCandy](https://github.com/NewLifeX/GitCandy)         | 2015  | 维护中 |    ×    | Git源代码管理系统                                                                    |
|                           NewLife.A2                           | 2019  |  商用  |    √    | 嵌入式工业计算机，物联网边缘网关，高性能.NET6主机，应用于工业、农业、交通、医疗       |
|                          NewLife.IoT                           | 2020  |  商用  |    √    | 物联网整体解决方案，建筑、环保、农业，软硬件及大数据分析一体化，单机十万级点位项目验证 |
|                          NewLife.UWB                          | 2020  |  商用  |    √    | 厘米级（10~20cm）高精度室内定位，软硬件一体化，与其它系统联动，大型展厅项目验证                 |

## 新生命开发团队
![XCode](https://newlifex.com/logo.png)  

新生命团队（NewLife）成立于2002年，是新时代物联网行业解决方案提供者，致力于提供软硬件应用方案咨询、系统架构规划与开发服务。  
团队主导的开源NewLife系列组件已被广泛应用于各行业，Nuget累计下载量高达60余万次。  
团队开发的大数据核心组件NewLife.XCode、蚂蚁调度计算平台AntJob、星尘分布式平台Stardust、缓存队列组件NewLife.Redis以及物联网平台NewLife.IoT，均成功应用于电力、高校、互联网、电信、交通、物流、工控、医疗、文博等行业，为客户提供了大量先进、可靠、安全、高质量、易扩展的产品和系统集成服务。  

我们将不断通过服务的持续改进，成为客户长期信赖的合作伙伴，通过不断的创新和发展，成为国内优秀的IT服务供应商。  

`新生命团队始于2002年，部分开源项目具有20年以上漫长历史，源码库保留有2010年以来所有修改记录`  
网站：https://newlifex.com  
开源：https://github.com/newlifex  
QQ群：1600800/1600838  
微信公众号：  
![智能大石头](https://newlifex.com/stone.jpg)  

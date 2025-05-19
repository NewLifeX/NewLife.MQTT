using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using NewLife.Data;
using NewLife.Log;
using NewLife.Model;
using NewLife.Net;

namespace NewLife.MQTT.WebSocket;

/// <summary>WebSocket协议编解码器</summary>
public class WebSocketCodec : Handler
{
    #region 属性
    /// <summary>是否已完成握手</summary>
    private Boolean _handshaked = false;

    /// <summary>日志</summary>
    private ILog _log;

    /// <summary>WebSocket路径</summary>
    private String _path = "/mqtt";

    /// <summary>WebSocket GUID，用于握手</summary>
    private const String WebSocketGuid = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";
    #endregion

    #region 构造函数
    /// <summary>实例化WebSocket编解码器</summary>
    /// <param name="log">日志对象</param>
    /// <param name="path">WebSocket路径</param>
    public WebSocketCodec(ILog log = null, String path = "/mqtt")
    {
        _log = log;
        _path = path;
    }
    #endregion

    #region 方法
    /// <summary>处理收到的数据</summary>
    /// <param name="context">处理上下文</param>
    /// <param name="message">消息</param>
    /// <returns></returns>
    public override Object Read(IHandlerContext context, Object message)
    {
        if (message is not IPacket pk) return base.Read(context, message);

        // 打印原始数据
        if (_log != null && _log.Level <= LogLevel.Debug)
        {
            _log.Debug($"【步骤1】WebSocket收到数据: {pk.ToHex(1024)}");
        }

        // 检查是否是WebSocket握手请求
        var data = pk.ToStr();
        if (data.StartsWith("GET") && data.Contains("Upgrade: websocket"))
        {
            _log.Debug($"【步骤2】WebSocket握手阶段");
            return HandleHandshake(context, pk);
        }

        // 如果还没有握手，但不是握手请求，转发给下一个处理器
        if (!_handshaked)
        {
            if (_log != null) _log.Debug($"【步骤1.1】未握手，但不是握手请求，转发给下一个处理器");
            return base.Read(context, message);
        }

        // 已握手，解析WebSocket数据帧
        _log.Debug($"【步骤3】WebSocket数据帧解析阶段");
        return DecodeWebSocketFrame(context, pk);
    }

    /// <summary>处理WebSocket握手</summary>
    /// <param name="context">处理上下文</param>
    /// <param name="pk">数据包</param>
    /// <returns></returns>
    private Object HandleHandshake(IHandlerContext context, IPacket pk)
    {
        try
        {
            // 如果已经握手，不再处理握手请求
            if (_handshaked)
            {
                if (_log != null) _log.Debug($"【步骤2.0】已经握手，不再处理握手请求");
                return DecodeWebSocketFrame(context, pk);
            }

            // 解析HTTP请求
            var request = pk.ToStr();
            if (_log != null) _log.Debug($"【步骤2.1】WebSocket握手请求:\r\n{request}");

            // 验证是否是WebSocket握手请求
            if (!request.Contains("Upgrade: websocket"))
            {
                if (_log != null) _log.Debug($"【步骤2.2】不是WebSocket握手请求，转发给下一个处理器");
                return base.Read(context, pk);
            }

            // 验证请求路径是否为配置的路径
            var requestLine = request.Split('\r', '\n')[0];
            if (_log != null) _log.Debug($"【步骤2.3】请求行: {requestLine}");

            if (!requestLine.StartsWith($"GET {_path} "))
            {
                if (_log != null) _log.Debug($"【步骤2.4】拒绝非{_path}路径的WebSocket请求: {requestLine}");

                // 发送404响应
                var sessionObj = (context.Owner as ISocketSession);
                if (sessionObj != null)
                {
                    sessionObj.Send($"HTTP/1.1 404 Not Found\r\nContent-Type: text/plain\r\n\r\nWebSocket path {_path} not found");
                    if (_log != null) _log.Debug($"【步骤2.5】已发送404响应");
                }

                return null;
            }

            // 提取Sec-WebSocket-Key
            var key = ExtractWebSocketKey(request);
            if (String.IsNullOrEmpty(key))
            {
                if (_log != null) _log.Debug("【步骤2.6】未找到WebSocket密钥");
                return base.Read(context, pk);
            }

            // 生成握手响应，传入请求以便提取子协议
            var response = GenerateHandshakeResponse(key, request);
            if (_log != null) _log.Debug($"【步骤2.7】WebSocket握手响应:\r\n{response}");

            // 发送握手响应
            var session = (context.Owner as ISocketSession);
            if (session != null)
            {
                session.Send(response);

                // 标记已握手
                _handshaked = true;

                if (_log != null) _log.Debug("【步骤2.8】WebSocket握手成功，连接已建立");
            }
            else
            {
                if (_log != null) _log.Debug("【步骤2.9】无法获取会话对象");
            }

            // 握手完成后不需要继续处理这个包
            return null;
        }
        catch (Exception ex)
        {
            if (_log != null) _log.Debug($"【步骤2.10】WebSocket握手异常: {ex.Message}");
            return base.Read(context, pk);
        }
    }

    /// <summary>从HTTP请求中提取WebSocket密钥</summary>
    /// <param name="request">HTTP请求</param>
    /// <returns>WebSocket密钥</returns>
    private String ExtractWebSocketKey(String request)
    {
        try
        {
            var match = Regex.Match(request, "Sec-WebSocket-Key: (.+?)\\r\\n");
            if (match.Success)
            {
                var key = match.Groups[1].Value.Trim();
                if (_log != null) _log.Debug($"提取到WebSocket密钥: {key}");
                return key;
            }

            if (_log != null) _log.Debug("未找到WebSocket密钥");
            return null;
        }
        catch (Exception ex)
        {
            if (_log != null) _log.Debug($"提取WebSocket密钥异常: {ex.Message}");
            return null;
        }
    }

    /// <summary>从HTTP请求中提取WebSocket子协议</summary>
    /// <param name="request">HTTP请求</param>
    /// <returns>WebSocket子协议</returns>
    private String ExtractWebSocketProtocol(String request)
    {
        try
        {
            var match = Regex.Match(request, "Sec-WebSocket-Protocol: (.+?)\\r\\n");
            if (match.Success)
            {
                var protocol = match.Groups[1].Value.Trim();
                if (_log != null) _log.Debug($"提取到WebSocket子协议: {protocol}");
                return protocol;
            }

            return null;
        }
        catch (Exception ex)
        {
            if (_log != null) _log.Debug($"提取WebSocket子协议异常: {ex.Message}");
            return null;
        }
    }

    /// <summary>生成WebSocket握手响应</summary>
    /// <param name="key">WebSocket密钥</param>
    /// <param name="request">HTTP请求</param>
    /// <returns>HTTP响应</returns>
    private String GenerateHandshakeResponse(String key, String request = null)
    {
        try
        {
            // 计算Sec-WebSocket-Accept
            var combined = key + WebSocketGuid;
            var bytes = Encoding.UTF8.GetBytes(combined);
            var hash = SHA1.Create().ComputeHash(bytes);
            var accept = Convert.ToBase64String(hash);

            if (_log != null) _log.Debug($"计算WebSocket Accept: {accept}");

            // 构建HTTP响应
            var response = new StringBuilder();
            response.AppendLine("HTTP/1.1 101 Switching Protocols");
            response.AppendLine("Upgrade: websocket");
            response.AppendLine("Connection: Upgrade");
            response.AppendLine($"Sec-WebSocket-Accept: {accept}");

            // 如果请求中包含子协议，则在响应中也包含
            if (!String.IsNullOrEmpty(request))
            {
                var protocol = ExtractWebSocketProtocol(request);
                if (!String.IsNullOrEmpty(protocol))
                {
                    // 只接受mqttv3.1和mqttv3.1.1子协议
                    if (protocol.Contains("mqttv3.1"))
                    {
                        response.AppendLine($"Sec-WebSocket-Protocol: {protocol}");
                        if (_log != null) _log.Debug($"添加WebSocket子协议到响应: {protocol}");
                    }
                    else
                    {
                        if (_log != null) _log.Debug($"不支持的WebSocket子协议: {protocol}");
                    }
                }
            }

            response.AppendLine();

            return response.ToString();
        }
        catch (Exception ex)
        {
            if (_log != null) _log.Debug($"生成WebSocket握手响应异常: {ex.Message}");
            return "HTTP/1.1 400 Bad Request\r\n\r\n";
        }
    }

    /// <summary>解析WebSocket数据帧</summary>
    /// <param name="context">处理上下文</param>
    /// <param name="pk">数据包</param>
    /// <returns>解析后的数据</returns>
    private Object DecodeWebSocketFrame(IHandlerContext context, IPacket pk)
    {
        var data = pk.GetSpan();
        if (data.Length < 2)
        {
            if (_log != null) _log.Debug($"【步骤3.1】WebSocket数据帧长度不足");
            return null;
        }

        // 解析WebSocket帧头
        var fin = (data[0] & 0x80) != 0;
        var mask = (data[1] & 0x80) != 0;
        var opcode = data[0] & 0x0F;
        var payloadLen = data[1] & 0x7F;

        if (_log != null) _log.Debug($"【步骤3.2】WebSocket帧头: FIN={fin}, Opcode={opcode}, Mask={mask}, PayloadLen={payloadLen}");

        var headerLen = 2;
        if (payloadLen == 126)
        {
            if (data.Length < 4)
            {
                if (_log != null) _log.Debug($"【步骤3.3】WebSocket数据帧长度不足(126)");
                return null;
            }
            headerLen += 2;
            payloadLen = (data[2] << 8) | data[3];
            if (_log != null) _log.Debug($"【步骤3.4】WebSocket扩展长度(16位): {payloadLen}");
        }
        else if (payloadLen == 127)
        {
            if (data.Length < 10)
            {
                if (_log != null) _log.Debug($"【步骤3.5】WebSocket数据帧长度不足(127)");
                return null;
            }
            headerLen += 8;
            payloadLen = 0; // 简化处理，实际应该读取8字节长度
            for (var i = 0; i < 8; i++)
            {
                payloadLen = (payloadLen << 8) | data[2 + i];
            }
            if (_log != null) _log.Debug($"【步骤3.6】WebSocket扩展长度(64位): {payloadLen}");
        }

        Byte[] maskKey = null;
        if (mask)
        {
            if (data.Length < headerLen + 4)
            {
                if (_log != null) _log.Debug($"【步骤3.7】WebSocket数据帧长度不足(掩码)");
                return null;
            }
            maskKey = new Byte[4];
            for (var i = 0; i < 4; i++)
            {
                maskKey[i] = data[headerLen + i];
            }
            headerLen += 4;
            if (_log != null) _log.Debug($"【步骤3.8】WebSocket掩码: {BitConverter.ToString(maskKey)}");
        }

        // 检查数据长度是否足够
        if (data.Length < headerLen + payloadLen)
        {
            if (_log != null) _log.Debug($"【步骤3.9】WebSocket数据帧长度不足(负载): {data.Length} < {headerLen + payloadLen}");
            return null;
        }

        // 提取有效载荷
        var payload = new Byte[payloadLen];
        for (var i = 0; i < payloadLen; i++)
        {
            payload[i] = mask ? (Byte)(data[headerLen + i] ^ maskKey[i % 4]) : data[headerLen + i];
        }

        // 打印解析后的WebSocket帧信息
        if (_log != null)
        {
            _log.Debug($"【步骤3.10】WebSocket帧解析完成: FIN={fin}, Opcode={opcode}, Mask={mask}, PayloadLen={payloadLen}");
            _log.Debug($"【步骤3.11】WebSocket负载(MQTT数据): {BitConverter.ToString(payload)}");

            // 尝试解析MQTT数据
            if (payload.Length > 0)
            {
                var mqttType = (payload[0] >> 4) & 0x0F;
                _log.Debug($"【步骤4】MQTT消息类型: {mqttType}");

                // 根据MQTT消息类型打印更详细的信息
                switch (mqttType)
                {
                    case 1:
                        _log.Debug($"【步骤4.1】MQTT CONNECT包");
                        // 解析CONNECT包
                        if (payload.Length >= 2)
                        {
                            // 解析剩余长度
                            Int32 remainingLength = payload[1];
                            var multiplier = 1;
                            var index = 2;
                            while (index < payload.Length && (payload[index - 1] & 0x80) != 0)
                            {
                                remainingLength += (payload[index] & 0x7F) * multiplier;
                                multiplier *= 128;
                                index++;
                            }

                            _log.Debug($"【步骤4.1.1】MQTT CONNECT包剩余长度: {remainingLength}");

                            // 解析协议名称长度
                            if (index + 1 < payload.Length)
                            {
                                var protocolNameLength = (payload[index] << 8) | payload[index + 1];
                                index += 2;

                                _log.Debug($"【步骤4.1.2】MQTT协议名称长度: {protocolNameLength}");

                                // 解析协议名称
                                if (index + protocolNameLength <= payload.Length)
                                {
                                    var protocolName = Encoding.UTF8.GetString(payload, index, protocolNameLength);
                                    index += protocolNameLength;

                                    _log.Debug($"【步骤4.1.3】MQTT协议名称: {protocolName}");

                                    // 解析协议版本
                                    if (index < payload.Length)
                                    {
                                        var protocolVersion = payload[index];
                                        index++;

                                        _log.Debug($"【步骤4.1.4】MQTT协议版本: {protocolVersion}");

                                        // 解析连接标志
                                        if (index < payload.Length)
                                        {
                                            var connectFlags = payload[index];
                                            index++;

                                            var cleanSession = (connectFlags & 0x02) != 0;
                                            var willFlag = (connectFlags & 0x04) != 0;
                                            var willQos = (connectFlags >> 3) & 0x03;
                                            var willRetain = (connectFlags & 0x20) != 0;
                                            var passwordFlag = (connectFlags & 0x40) != 0;
                                            var usernameFlag = (connectFlags & 0x80) != 0;

                                            _log.Debug($"【步骤4.1.5】MQTT连接标志: 清除会话={cleanSession}, 遗嘱标志={willFlag}, 遗嘱QoS={willQos}, 遗嘱保留={willRetain}, 密码标志={passwordFlag}, 用户名标志={usernameFlag}");

                                            // 解析保持连接时间
                                            if (index + 1 < payload.Length)
                                            {
                                                var keepAlive = (payload[index] << 8) | payload[index + 1];
                                                index += 2;

                                                _log.Debug($"【步骤4.1.6】MQTT保持连接时间: {keepAlive}秒");

                                                // 解析客户端ID长度
                                                if (index + 1 < payload.Length)
                                                {
                                                    var clientIdLength = (payload[index] << 8) | payload[index + 1];
                                                    index += 2;

                                                    _log.Debug($"【步骤4.1.7】MQTT客户端ID长度: {clientIdLength}");

                                                    // 解析客户端ID
                                                    if (index + clientIdLength <= payload.Length)
                                                    {
                                                        var clientId = Encoding.UTF8.GetString(payload, index, clientIdLength);
                                                        index += clientIdLength;

                                                        _log.Debug($"【步骤4.1.8】MQTT客户端ID: {clientId}");
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        break;
                    case 2:
                        _log.Debug($"【步骤4.2】MQTT CONNACK包");
                        break;
                    case 3:
                        _log.Debug($"【步骤4.3】MQTT PUBLISH包");
                        break;
                    case 4:
                        _log.Debug($"【步骤4.4】MQTT PUBACK包");
                        break;
                    case 8:
                        _log.Debug($"【步骤4.5】MQTT SUBSCRIBE包");
                        break;
                    case 9:
                        _log.Debug($"【步骤4.6】MQTT SUBACK包");
                        break;
                    case 12:
                        _log.Debug($"【步骤4.7】MQTT PINGREQ包");
                        break;
                    case 13:
                        _log.Debug($"【步骤4.8】MQTT PINGRESP包");
                        break;
                    case 14:
                        _log.Debug($"【步骤4.9】MQTT DISCONNECT包");
                        break;
                    default:
                        _log.Debug($"【步骤4.10】未知MQTT消息类型: {mqttType}");
                        break;
                }
            }
        }

        // 根据操作码处理
        switch (opcode)
        {
            case 0x1: // 文本帧
                if (_log != null) _log.Debug($"【步骤3.12】WebSocket文本帧，转发给MQTT处理器");
                // 创建新的数据包，传递给下一个处理器
                return new ArrayPacket(payload);

            case 0x2: // 二进制帧
                if (_log != null) _log.Debug($"【步骤3.13】WebSocket二进制帧，转发给MQTT处理器");
                // 创建新的数据包，传递给下一个处理器
                return new ArrayPacket(payload);

            case 0x8: // 关闭帧
                      // 发送关闭帧响应
                if (_log != null) _log.Debug("【步骤3.14】收到WebSocket关闭帧");
                var session = (context.Owner as ISocketSession);
                if (session != null)
                {
                    // 构建关闭帧
                    var frame = new Byte[] { 0x88, 0x00 }; // FIN=1, Opcode=8, Payload=0
                    session.Send(frame);
                    if (_log != null) _log.Debug("【步骤3.15】已发送WebSocket关闭帧响应");
                }
                return null;

            case 0x9: // Ping帧
                      // 发送Pong帧响应
                if (_log != null) _log.Debug("【步骤3.16】收到WebSocket Ping帧");
                var session2 = (context.Owner as ISocketSession);
                if (session2 != null)
                {
                    // 构建Pong帧
                    var frame = new Byte[] { 0x8A, 0x00 }; // FIN=1, Opcode=10, Payload=0
                    session2.Send(frame);
                    if (_log != null) _log.Debug("【步骤3.17】已发送WebSocket Pong帧响应");
                }
                return null;

            default:
                if (_log != null) _log.Debug($"【步骤3.18】收到未知WebSocket帧类型: {opcode}");
                return null;
        }
    }



    /// <summary>处理发送数据</summary>
    /// <param name="context">处理上下文</param>
    /// <param name="message">消息</param>
    /// <returns></returns>
    public override Object Write(IHandlerContext context, Object message)
    {
        if (!_handshaked || message is not IPacket pk) return base.Write(context, message);

        // 打印要发送的数据
        if (_log != null && _log.Level <= LogLevel.Debug)
        {
            _log.Debug($"【步骤5】WebSocket发送数据(MQTT响应): {pk.ToHex(1024)}");

            // 尝试解析MQTT数据
            if (pk.Total > 0)
            {
                var data = pk.GetSpan();
                var mqttType = (data[0] >> 4) & 0x0F;
                _log.Debug($"【步骤5.1】MQTT响应类型: {mqttType}");

                // 根据MQTT消息类型打印更详细的信息
                switch (mqttType)
                {
                    case 1:
                        _log.Debug($"【步骤5.2】MQTT CONNECT响应");
                        break;
                    case 2:
                        _log.Debug($"【步骤5.3】MQTT CONNACK响应");
                        break;
                    case 3:
                        _log.Debug($"【步骤5.4】MQTT PUBLISH响应");
                        break;
                    case 4:
                        _log.Debug($"【步骤5.5】MQTT PUBACK响应");
                        break;
                    case 8:
                        _log.Debug($"【步骤5.6】MQTT SUBSCRIBE响应");
                        break;
                    case 9:
                        _log.Debug($"【步骤5.7】MQTT SUBACK响应");
                        break;
                    case 12:
                        _log.Debug($"【步骤5.8】MQTT PINGREQ响应");
                        break;
                    case 13:
                        _log.Debug($"【步骤5.9】MQTT PINGRESP响应");
                        break;
                    case 14:
                        _log.Debug($"【步骤5.10】MQTT DISCONNECT响应");
                        break;
                    default:
                        _log.Debug($"【步骤5.11】未知MQTT响应类型: {mqttType}");
                        break;
                }
            }
        }

        // 封装为WebSocket帧
        _log.Debug($"【步骤6】封装WebSocket帧");
        return EncodeWebSocketFrame(pk);
    }

    /// <summary>封装WebSocket数据帧</summary>
    /// <param name="pk">数据包</param>
    /// <returns>WebSocket帧</returns>
    private Object EncodeWebSocketFrame(IPacket pk)
    {
        var data = pk.GetSpan();
        var length = data.Length;

        if (_log != null) _log.Debug($"【步骤6.1】开始封装WebSocket帧，数据长度={length}");

        // 计算帧头长度
        var headerLength = 2;
        if (length > 125 && length <= 65535)
        {
            headerLength += 2;
            if (_log != null) _log.Debug($"【步骤6.2】使用16位长度字段，帧头长度={headerLength}");
        }
        else if (length > 65535)
        {
            headerLength += 8;
            if (_log != null) _log.Debug($"【步骤6.3】使用64位长度字段，帧头长度={headerLength}");
        }
        else
        {
            if (_log != null) _log.Debug($"【步骤6.4】使用7位长度字段，帧头长度={headerLength}");
        }

        // 创建帧
        var frame = new Byte[headerLength + length];

        // 设置帧头
        frame[0] = 0x82; // FIN=1, Opcode=2 (二进制)

        if (length <= 125)
        {
            frame[1] = (Byte)length;
            if (_log != null) _log.Debug($"【步骤6.5】设置7位长度字段: {length}");
        }
        else if (length <= 65535)
        {
            frame[1] = 126;
            frame[2] = (Byte)(length >> 8);
            frame[3] = (Byte)length;
            if (_log != null) _log.Debug($"【步骤6.6】设置16位长度字段: {length} = {frame[2]:X2} {frame[3]:X2}");
        }
        else
        {
            frame[1] = 127;
            for (var i = 0; i < 8; i++)
            {
                frame[2 + i] = (Byte)(length >> ((7 - i) * 8));
            }
            if (_log != null) _log.Debug($"【步骤6.7】设置64位长度字段: {length}");
        }

        // 复制数据
        for (var i = 0; i < length; i++)
        {
            frame[headerLength + i] = data[i];
        }

        if (_log != null && _log.Level <= LogLevel.Debug)
        {
            _log.Debug($"【步骤6.8】WebSocket帧封装完成: 数据长度={length}, 帧总长度={frame.Length}");
            _log.Debug($"【步骤6.9】WebSocket帧头: {BitConverter.ToString(frame, 0, Math.Min(headerLength, 10))}");
        }

        return new ArrayPacket(frame);
    }
    #endregion
}
namespace NewLife.MQTT.Messaging;

/// <summary>MQTT协议版本</summary>
public enum MqttVersion : Byte
{
    /// <summary>MQTT 3.1</summary>
    V310 = 0x03,

    /// <summary>MQTT 3.1.1（默认）</summary>
    V311 = 0x04,

    /// <summary>MQTT 5.0</summary>
    V500 = 0x05
}

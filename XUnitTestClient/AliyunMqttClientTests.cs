using System.Threading.Tasks;
using NewLife.Log;
using NewLife.MQTT;
using NewLife.Security;
using Xunit;

namespace XUnitTestClient;

public class AliyunMqttClientTests
{
    [Fact]
    public async Task Test3()
    {
        var client = new AliyunMqttClient("a18RQ72tLHD", "dev1", "6oSl3CjHKM13J50DVVWNF3WbWWJjhAUf");
        client.Log = XTrace.Log;
        client.Server = $"tcp://{client.ProductKey}.iot-as-mqtt.cn-shanghai.aliyuncs.com:443";

        await client.ConnectAsync();
        await client.SyncTime();
        await client.PostProperty(new
        {
            // 温度
            Temperature = Rand.Next(-4000, 120_00) / 100d,
            // 相对湿度
            RelativeHumidity = Rand.Next(0, 100_00) / 100d,
            // 风向
            WindDirection = Rand.Next(0, 360_00) / 100d,
            // 氟化物浓度
            Fluoride = Rand.Next(0, 10000_00) / 100d,
            // 空气质量指数
            AQI = Rand.Next(0, 500),
            // 首要污染物
            PrimaryItem = Rand.NextString(32),
            // 地理位置
            GeoLocation = new
            {
                Longitude = Rand.Next(-180_00, 180_00) / 100d,
                Latitude = Rand.Next(-180_00, 180_00) / 100d,
                Altitude = Rand.Next(0, 10000_00) / 100d,
                // 1=WGS_84, 2=GCJ_02
                CoordinateSystem = 1,
            },
        });
    }
}

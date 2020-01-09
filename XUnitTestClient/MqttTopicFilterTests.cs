using System;
using System.Collections.Generic;
using System.Text;
using NewLife.MQTT;
using Xunit;

namespace XUnitTestClient
{
    public class MqttTopicFilterTests
    {
        [Fact]
        public void Test()
        {
            var sub = new string[] { "/test/#", "/test/+/test/test", "/test/+/#" };

            var pub = "/test/test/test/test";
            foreach (var item in sub)
            {
                Assert.True(MqttTopicFilter.Matches(pub, item));
            }
            var sub1 = new string[] { "test/#", "/test/sss/test/test", "/test//#" };

            foreach (var item in sub1)
            {
                Assert.False(MqttTopicFilter.Matches(pub, item));
            }
        }
    }
}

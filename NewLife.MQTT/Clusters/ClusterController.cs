using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NewLife.Remoting;

namespace NewLife.MQTT.Clusters;

public class ClusterController : IApi
{
    public IApiSession Session { get; set; }
}

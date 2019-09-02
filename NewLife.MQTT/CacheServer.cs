using NewLife.Remoting;

namespace NewLife.Caching
{
    /// <summary>缓存服务器</summary>
    public class CacheServer : ApiServer
    {
        /// <summary>启动时</summary>
        public override void Start()
        {
            if (Manager.Services.Count <= 2) Register(new CacheService(), null);

            base.Start();
        }
    }
}
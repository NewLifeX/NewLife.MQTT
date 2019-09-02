using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NewLife.Collections;
using NewLife.Data;
using NewLife.Reflection;
using NewLife.Remoting;
using NewLife.Serialization;
#if !NET4
using TaskEx = System.Threading.Tasks.Task;
#endif

namespace NewLife.Caching
{
    /// <summary>缓存客户端。对接缓存服务端CacheServer</summary>
    public class CacheClient : Cache
    {
        #region 属性
        /// <summary>客户端</summary>
        public ApiClient Client { get; set; }
        #endregion

        #region 远程操作
        /// <summary>设置服务端地址。支持多地址负载均衡</summary>
        /// <param name="servers"></param>
        /// <returns></returns>
        public ApiClient SetServer(params String[] servers)
        {
            var ac = Client ?? new ApiClient();
            ac.Servers = servers;

            //if (ac.Encoder == null) ac.Encoder = new BinaryEncoder();

            Client = ac;

            return ac;
        }

        /// <summary>调用</summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="action"></param>
        /// <param name="args"></param>
        /// <returns></returns>
        protected virtual T Invoke<T>(String action, Object args = null) => TaskEx.Run(() => Client.InvokeAsync<T>(action, args)).Result;
        #endregion

        #region 基础操作
        /// <summary>初始化配置</summary>
        /// <param name="config"></param>
        public override void Init(String config) { }

        /// <summary>缓存个数</summary>
        public override Int32 Count
        {
            get
            {
                var rs = Invoke<Packet>(nameof(Count));
                if (rs == null || rs.Total == 0) return 0;

                return rs.ReadBytes(0, 4).ToInt();
            }
        }

        /// <summary>所有键</summary>
        public override ICollection<String> Keys
        {
            get
            {
                var rs = Invoke<Packet>(nameof(Keys));
                if (rs == null || rs.Total == 0) return new String[0];

                var keys = new List<String>();
                var ms = rs.GetStream();
                while (ms.Position < ms.Length)
                {
                    var key = ms.ReadArray().ToStr();
                    keys.Add(key);
                }

                return keys;
            }
        }

        /// <summary>是否包含缓存项</summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public override Boolean ContainsKey(String key)
        {
            var rs = Invoke<Packet>(nameof(ContainsKey), key.GetBytes());
            return rs != null && rs.Total > 0 && rs[0] > 0;
        }

        /// <summary>设置缓存项</summary>
        /// <param name="key">键</param>
        /// <param name="value">值</param>
        /// <param name="expire">过期时间，秒。小于0时采用默认缓存时间Expire</param>
        /// <returns></returns>
        public override Boolean Set<T>(String key, T value, Int32 expire = -1)
        {
            var ms = Pool.MemoryStream.Get();
            var bn = new Binary { Stream = ms };
            bn.Write(key);
            bn.Write(expire);

            // 基元类型只写，复杂类型Json
            var type = value.GetType();
            if (type.GetTypeCode() != TypeCode.Object)
                bn.Write(value);
            else if (value is IAccessor acc)
                acc.Write(ms, bn);
            else
                bn.Write(value.ToJson());

            var rs = Invoke<Packet>(nameof(Set), ms.Put(true));
            return rs != null && rs.Total > 0 && rs[0] > 0;
        }

        /// <summary>获取缓存项</summary>
        /// <param name="key">键</param>
        /// <returns></returns>
        public override T Get<T>(String key)
        {
            var rs = Invoke<Packet>(nameof(Get), key.GetBytes());
            if (rs == null || rs.Total == 0) return default(T);

            var type = typeof(T);
            if (type == typeof(Object) || type == typeof(Packet)) return (T)(Object)rs;
            if (type == typeof(Byte[])) return (T)(Object)rs.ReadBytes();

            return Binary.FastRead<T>(rs.GetStream(), false);
        }

        /// <summary>批量移除缓存项</summary>
        /// <param name="keys">键集合</param>
        /// <returns></returns>
        public override Int32 Remove(params String[] keys)
        {
            var ms = Pool.MemoryStream.Get();
            foreach (var item in keys)
            {
                ms.WriteArray(item.GetBytes());
            }

            var rs = Invoke<Packet>(nameof(Remove), ms.Put(true));
            if (rs == null || rs.Total == 0) return 0;

            return rs.ReadBytes(0, 4).ToInt();
        }

        /// <summary>设置缓存项有效期</summary>
        /// <param name="key">键</param>
        /// <param name="expire">过期时间，秒</param>
        public override Boolean SetExpire(String key, TimeSpan expire)
        {
            var ms = Pool.MemoryStream.Get();
            ms.WriteArray(key.GetBytes());
            ms.Write(((Int32)expire.TotalSeconds).GetBytes());

            var rs = Invoke<Packet>(nameof(SetExpire), ms.Put(true));
            return rs != null && rs.Total > 0 && rs[0] > 0;
        }

        /// <summary>获取缓存项有效期</summary>
        /// <param name="key">键</param>
        /// <returns></returns>
        public override TimeSpan GetExpire(String key)
        {
            var rs = Invoke<Packet>(nameof(GetExpire), key.GetBytes());
            return TimeSpan.FromSeconds(rs.ReadBytes(0, 4).ToInt());
        }
        #endregion

        #region 集合操作
        /// <summary>批量获取缓存项</summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="keys"></param>
        /// <returns></returns>
        public override IDictionary<String, T> GetAll<T>(IEnumerable<String> keys)
        {
            var ms = Pool.MemoryStream.Get();
            foreach (var item in keys)
            {
                ms.WriteArray(item.GetBytes());
            }

            var dic = new Dictionary<String, T>();

            var rs = Invoke<Packet>(nameof(GetAll), ms.Put(true));
            if (rs == null || rs.Total == 0) return dic;

            ms = rs.GetStream();
            var bn = new Binary { Stream = ms };
            while (ms.Position < ms.Length)
            {
                var key = bn.Read<String>();
                var value = bn.Read<T>();
                dic[key] = value;
            }

            return dic;
        }

        /// <summary>批量设置缓存项</summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="values"></param>
        /// <param name="expire">过期时间，秒。小于0时采用默认缓存时间Expire</param>
        public override void SetAll<T>(IDictionary<String, T> values, Int32 expire = -1)
        {
            Invoke<IDictionary<String, T>>(nameof(SetAll), new { values, expire });
        }
        #endregion

        #region 高级操作
        /// <summary>添加，已存在时不更新</summary>
        /// <typeparam name="T">值类型</typeparam>
        /// <param name="key">键</param>
        /// <param name="value">值</param>
        /// <param name="expire">过期时间，秒。小于0时采用默认缓存时间<seealso cref="Cache.Expire"/></param>
        /// <returns></returns>
        public override Boolean Add<T>(String key, T value, Int32 expire = -1)
        {
            var ms = Pool.MemoryStream.Get();
            var bn = new Binary { Stream = ms };
            bn.Write(key);
            bn.Write(expire);
            bn.Write(value);

            var rs = Invoke<Packet>(nameof(Add), ms.Put(true));
            return rs != null && rs.Total > 0 && rs[0] > 0;
        }

        /// <summary>设置新值并获取旧值，原子操作</summary>
        /// <typeparam name="T">值类型</typeparam>
        /// <param name="key">键</param>
        /// <param name="value">值</param>
        /// <returns></returns>
        public override T Replace<T>(String key, T value)
        {
            var ms = Pool.MemoryStream.Get();
            var bn = new Binary { Stream = ms };
            bn.Write(key);
            bn.Write(value);

            var rs = Invoke<Packet>(nameof(Replace), ms.Put(true));
            if (rs == null || rs.Total == 0) return default(T);

            return Binary.FastRead<T>(rs.GetStream(), false);
        }

        /// <summary>累加，原子操作</summary>
        /// <param name="key">键</param>
        /// <param name="value">变化量</param>
        /// <returns></returns>
        public override Int64 Increment(String key, Int64 value)
        {
            var ms = Pool.MemoryStream.Get();
            ms.WriteArray(key.GetBytes());
            ms.Write(value.GetBytes());

            var rs = Invoke<Packet>(nameof(Increment), ms.Put(true));
            if (rs == null || rs.Total == 0) return 0;

            return rs.ReadBytes(0, 8).ToLong();
        }

        /// <summary>累加，原子操作</summary>
        /// <param name="key">键</param>
        /// <param name="value">变化量</param>
        /// <returns></returns>
        public override Double Increment(String key, Double value)
        {
            var ms = Pool.MemoryStream.Get();
            ms.WriteArray(key.GetBytes());
            ms.Write(BitConverter.GetBytes(value));

            var rs = Invoke<Packet>(nameof(Increment) + "2", ms.Put(true));
            if (rs == null || rs.Total == 0) return 0;

            return rs.ReadBytes(0, 8).ToDouble();
        }

        /// <summary>递减，原子操作</summary>
        /// <param name="key">键</param>
        /// <param name="value">变化量</param>
        /// <returns></returns>
        public override Int64 Decrement(String key, Int64 value)
        {
            var ms = Pool.MemoryStream.Get();
            ms.WriteArray(key.GetBytes());
            ms.Write(value.GetBytes());

            var rs = Invoke<Packet>(nameof(Decrement), ms.Put(true));
            if (rs == null || rs.Total == 0) return 0;

            return rs.ReadBytes(0, 8).ToLong();
        }

        /// <summary>递减，原子操作</summary>
        /// <param name="key">键</param>
        /// <param name="value">变化量</param>
        /// <returns></returns>
        public override Double Decrement(String key, Double value)
        {
            var ms = Pool.MemoryStream.Get();
            ms.WriteArray(key.GetBytes());
            ms.Write(BitConverter.GetBytes(value));

            var rs = Invoke<Packet>(nameof(Decrement) + "2", ms.Put(true));
            if (rs == null || rs.Total == 0) return 0;

            return rs.ReadBytes(0, 8).ToDouble();
        }
        #endregion
    }
}
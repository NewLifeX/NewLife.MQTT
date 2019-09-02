using System;
using System.Collections.Generic;
using NewLife.Collections;
using NewLife.Data;
using NewLife.Remoting;
using NewLife.Serialization;

namespace NewLife.Caching
{
    /// <summary>缓存服务</summary>
    [Api(null)]
    public class CacheService
    {
        #region 属性
        /// <summary>适配对象</summary>
        public ICache Cache { get; set; } = MemoryCache.Default;
        #endregion

        #region 基础操作
        /// <summary>缓存个数</summary>
        [Api(nameof(Count))]
        public Packet Count() => Cache.Count.GetBytes();

        /// <summary>所有键</summary>
        [Api(nameof(Keys))]
        public Packet Keys()
        {
            var ks = Cache.Keys;

            var ms = Pool.MemoryStream.Get();
            foreach (var item in ks)
            {
                ms.WriteArray(item.GetBytes());
            }

            return ms.Put(true);
        }

        /// <summary>是否包含缓存项</summary>
        /// <param name="key">键</param>
        /// <returns></returns>
        [Api(nameof(ContainsKey))]
        public Packet ContainsKey(Packet key)
        {
            var rs = Cache.ContainsKey(key.ToStr());
            return new[] { (Byte)(rs ? 1 : 0) };
        }

        /// <summary>设置缓存项</summary>
        /// <param name="data">参数</param>
        /// <returns></returns>
        [Api(nameof(Set))]
        public Packet Set(Packet data)
        {
            var ms = data.GetStream();
            var key = ms.ReadArray().ToStr();
            var expire = ms.ReadBytes(4).ToInt();
            var value = ms.ReadBytes();

            var rs = Cache.Set(key, value, expire);
            return new[] { (Byte)(rs ? 1 : 0) };
        }

        /// <summary>获取缓存项</summary>
        /// <param name="key">键</param>
        /// <returns></returns>
        [Api(nameof(Get))]
        public Packet Get(Packet key) => Cache.Get<Byte[]>(key.ToStr());

        /// <summary>批量移除缓存项</summary>
        /// <param name="data">数据</param>
        /// <returns></returns>
        [Api(nameof(Remove))]
        public Packet Remove(Packet data)
        {
            var keys = new List<String>();
            var ms = data.GetStream();
            while (ms.Position < ms.Length)
            {
                keys.Add(ms.ReadArray().ToStr());
            }

            return Cache.Remove(keys.ToArray()).GetBytes();
        }

        /// <summary>设置缓存项有效期</summary>
        /// <param name="data">数据</param>
        [Api(nameof(SetExpire))]
        public Packet SetExpire(Packet data)
        {
            var ms = data.GetStream();
            var key = ms.ReadArray().ToStr();
            var expire = ms.ReadBytes(4).ToInt();

            var rs = Cache.SetExpire(key, TimeSpan.FromSeconds(expire));
            return new[] { (Byte)(rs ? 1 : 0) };
        }

        /// <summary>获取缓存项有效期</summary>
        /// <param name="key">键</param>
        /// <returns></returns>
        [Api(nameof(GetExpire))]
        public Packet GetExpire(Packet key)
        {
            var rs = (Int64)Cache.GetExpire(key.ToStr()).TotalSeconds;
            return rs.GetBytes();
        }
        #endregion

        #region 集合操作
        /// <summary>批量获取缓存项</summary>
        /// <param name="data">数据</param>
        /// <returns></returns>
        [Api(nameof(GetAll))]
        public Packet GetAll(Packet data)
        {
            var keys = new List<String>();
            var ms = data.GetStream();
            while (ms.Position < ms.Length)
            {
                keys.Add(ms.ReadArray().ToStr());
            }

            var dic = Cache.GetAll<Object>(keys);
            ms = Pool.MemoryStream.Get();
            var bn = new Binary { Stream = ms };
            foreach (var item in dic)
            {
                bn.Write(item.Key);
                bn.Write(item.Value);
            }

            return ms.Put(true);
        }

        /// <summary>批量设置缓存项</summary>
        /// <param name="values"></param>
        /// <param name="expire">过期时间，秒。小于0时采用默认缓存时间Expire</param>
        [Api(nameof(SetAll))]
        public void SetAll(IDictionary<String, Object> values, Int32 expire = -1) => Cache.SetAll(values, expire);
        #endregion

        #region 高级操作
        /// <summary>添加，已存在时不更新</summary>
        /// <param name="data">数据</param>
        /// <returns></returns>
        [Api(nameof(Add))]
        public Packet Add(Packet data)
        {
            var ms = data.GetStream();
            var key = ms.ReadArray().ToStr();
            var expire = ms.ReadBytes(4).ToInt();
            var value = ms.ReadBytes();

            var rs = Cache.Add(key, value, expire);
            return new[] { (Byte)(rs ? 1 : 0) };
        }

        /// <summary>设置新值并获取旧值，原子操作</summary>
        /// <param name="data">数据</param>
        /// <returns></returns>
        [Api(nameof(Replace))]
        public Packet Replace(Packet data)
        {
            var ms = data.GetStream();
            var key = ms.ReadArray().ToStr();
            var value = ms.ReadBytes();

            return Cache.Replace(key, value);
        }

        /// <summary>累加，原子操作</summary>
        /// <param name="data">数据</param>
        /// <returns></returns>
        [Api(nameof(Increment))]
        public Packet Increment(Packet data)
        {
            var ms = data.GetStream();
            var key = ms.ReadArray().ToStr();
            var value = ms.ReadBytes(8).ToLong();

            var rs = Cache.Increment(key, value);
            return rs.GetBytes();
        }

        /// <summary>累加，原子操作</summary>
        /// <param name="data">数据</param>
        /// <returns></returns>
        [Api(nameof(Increment2))]
        public Packet Increment2(Packet data)
        {
            var ms = data.GetStream();
            var key = ms.ReadArray().ToStr();
            var value = ms.ReadBytes(8).ToDouble();

            var rs = Cache.Increment(key, value);
            return BitConverter.GetBytes(rs);
        }

        /// <summary>递减，原子操作</summary>
        /// <param name="data">数据</param>
        /// <returns></returns>
        [Api(nameof(Decrement))]
        public Packet Decrement(Packet data)
        {
            var ms = data.GetStream();
            var key = ms.ReadArray().ToStr();
            var value = ms.ReadBytes(8).ToLong();

            var rs = Cache.Decrement(key, value);
            return rs.GetBytes();
        }

        /// <summary>递减，原子操作</summary>
        /// <param name="data">数据</param>
        /// <returns></returns>
        [Api(nameof(Decrement2))]
        public Packet Decrement2(Packet data)
        {
            var ms = data.GetStream();
            var key = ms.ReadArray().ToStr();
            var value = ms.ReadBytes(8).ToDouble();

            var rs = Cache.Decrement(key, value);
            return BitConverter.GetBytes(rs);
        }
        #endregion
    }
}
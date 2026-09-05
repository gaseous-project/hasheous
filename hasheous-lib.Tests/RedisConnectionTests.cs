using System.Reflection;
using hasheous.Classes;

namespace hasheous_lib.Tests;

public class RedisConnectionTests
{
    [Theory]
    [InlineData(typeof(string), true)]
    [InlineData(typeof(int), true)]
    [InlineData(typeof(long), true)]
    [InlineData(typeof(bool), true)]
    [InlineData(typeof(double), true)]
    [InlineData(typeof(uint), true)]
    [InlineData(typeof(ulong), true)]
    [InlineData(typeof(byte[]), true)]
    [InlineData(typeof(decimal), false)]
    [InlineData(typeof(DateTime), false)]
    [InlineData(typeof(CacheValue), false)]
    public void CacheTypeClassificationMatchesStoragePolicy(Type type, bool expectedPrimitive)
    {
        Assert.Equal(expectedPrimitive, IsPrimitiveCacheType(type));
    }

    [Fact]
    public async Task SmallComplexValueUsesFramedPlainJson()
    {
        var value = new CacheValue { Name = "small", Count = 42 };

        byte[] payload = SerializeComplexCacheValue(value);
        CacheValue? roundTripped = await DeserializeComplexCacheValue<CacheValue>(payload);

        Assert.Equal(new byte[] { (byte)'H', (byte)'R', (byte)'C', 0 }, payload.Take(4));
        Assert.Equal(value.Name, roundTripped!.Name);
        Assert.Equal(value.Count, roundTripped.Count);
    }

    [Fact]
    public async Task LargeCompressibleComplexValueUsesFramedBrotli()
    {
        var value = new CacheValue { Name = new string('a', 5_000), Count = 7 };

        byte[] payload = SerializeComplexCacheValue(value);
        CacheValue? roundTripped = await DeserializeComplexCacheValue<CacheValue>(payload);

        Assert.Equal(new byte[] { (byte)'H', (byte)'R', (byte)'C', 1 }, payload.Take(4));
        Assert.Equal(value.Name, roundTripped!.Name);
        Assert.Equal(value.Count, roundTripped.Count);
    }

    [Theory]
    [InlineData(new byte[] { (byte)'{', (byte)'}' })]
    [InlineData(new byte[] { (byte)'H', (byte)'R', (byte)'C', 2 })]
    public async Task UnframedOrUnknownPayloadIsRejected(byte[] payload)
    {
        await Assert.ThrowsAsync<InvalidDataException>(() => DeserializeComplexCacheValue<CacheValue>(payload));
    }

    [Fact]
    public void BrotliRoundTripsJsonText()
    {
        const string json = "{\"Name\":\"cache value\",\"Count\":42}";

        string decompressed = RedisConnection.DecompressToString(RedisConnection.CompressString(json));

        Assert.Equal(json, decompressed);
    }

    private static bool IsPrimitiveCacheType(Type type)
    {
        MethodInfo method = GetRedisMethod("IsPrimitiveCacheType").MakeGenericMethod(type);
        return Assert.IsType<bool>(method.Invoke(null, null));
    }

    private static byte[] SerializeComplexCacheValue<T>(T value)
    {
        return Assert.IsType<byte[]>(GetRedisMethod("SerializeComplexCacheValue").MakeGenericMethod(typeof(T)).Invoke(null, new object?[] { value }));
    }

    private static Task<T?> DeserializeComplexCacheValue<T>(byte[] payload)
    {
        return Assert.IsType<Task<T?>>(GetRedisMethod("DeserializeComplexCacheValue").MakeGenericMethod(typeof(T)).Invoke(null, new object[] { payload }));
    }

    private static MethodInfo GetRedisMethod(string name)
    {
        return typeof(RedisConnection).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static)!;
    }

    private sealed class CacheValue
    {
        public string Name { get; set; } = string.Empty;
        public int Count { get; set; }
    }
}
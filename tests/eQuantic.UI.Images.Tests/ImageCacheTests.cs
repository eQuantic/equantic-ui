namespace eQuantic.UI.Images.Tests;

public class ImageCacheTests : IDisposable
{
    private readonly string _cacheDir;
    private readonly ImageCache _cache;

    public ImageCacheTests()
    {
        _cacheDir = Path.Combine(Path.GetTempPath(), $"equantic-test-cache-{Guid.NewGuid():N}");
        _cache = new ImageCache(new ImageOptimizationOptions
        {
            CacheDirectory = _cacheDir,
            CacheTtlSeconds = 3600
        });
    }

    public void Dispose()
    {
        if (Directory.Exists(_cacheDir))
            Directory.Delete(_cacheDir, true);
    }

    [Fact]
    public async Task GetAsync_EmptyCache_ReturnsNull()
    {
        var result = await _cache.GetAsync("/img.jpg", 640, 75, "image/webp");

        result.Should().BeNull();
    }

    [Fact]
    public async Task SetAsync_ThenGetAsync_ReturnsCachedData()
    {
        var data = new byte[] { 1, 2, 3, 4, 5 };

        await _cache.SetAsync("/img.jpg", 640, 75, "image/webp", data);
        var result = await _cache.GetAsync("/img.jpg", 640, 75, "image/webp");

        result.Should().BeEquivalentTo(data);
    }

    [Fact]
    public async Task GetAsync_DifferentWidth_ReturnsDifferentCache()
    {
        var data640 = new byte[] { 1, 2, 3 };
        var data1080 = new byte[] { 4, 5, 6 };

        await _cache.SetAsync("/img.jpg", 640, 75, "image/webp", data640);
        await _cache.SetAsync("/img.jpg", 1080, 75, "image/webp", data1080);

        var result640 = await _cache.GetAsync("/img.jpg", 640, 75, "image/webp");
        var result1080 = await _cache.GetAsync("/img.jpg", 1080, 75, "image/webp");

        result640.Should().BeEquivalentTo(data640);
        result1080.Should().BeEquivalentTo(data1080);
    }

    [Fact]
    public async Task GetAsync_DifferentQuality_ReturnsDifferentCache()
    {
        var dataQ50 = new byte[] { 10, 20 };
        var dataQ90 = new byte[] { 30, 40 };

        await _cache.SetAsync("/img.jpg", 640, 50, "image/webp", dataQ50);
        await _cache.SetAsync("/img.jpg", 640, 90, "image/webp", dataQ90);

        var resultQ50 = await _cache.GetAsync("/img.jpg", 640, 50, "image/webp");
        var resultQ90 = await _cache.GetAsync("/img.jpg", 640, 90, "image/webp");

        resultQ50.Should().BeEquivalentTo(dataQ50);
        resultQ90.Should().BeEquivalentTo(dataQ90);
    }

    [Fact]
    public async Task GetAsync_DifferentFormat_ReturnsDifferentCache()
    {
        var dataWebp = new byte[] { 1, 1, 1 };
        var dataJpeg = new byte[] { 2, 2, 2 };

        await _cache.SetAsync("/img.jpg", 640, 75, "image/webp", dataWebp);
        await _cache.SetAsync("/img.jpg", 640, 75, "image/jpeg", dataJpeg);

        var resultWebp = await _cache.GetAsync("/img.jpg", 640, 75, "image/webp");
        var resultJpeg = await _cache.GetAsync("/img.jpg", 640, 75, "image/jpeg");

        resultWebp.Should().BeEquivalentTo(dataWebp);
        resultJpeg.Should().BeEquivalentTo(dataJpeg);
    }

    [Fact]
    public async Task GetAsync_ExpiredEntry_ReturnsNull()
    {
        var shortTtlCache = new ImageCache(new ImageOptimizationOptions
        {
            CacheDirectory = _cacheDir,
            CacheTtlSeconds = 0 // Expire immediately
        });

        await shortTtlCache.SetAsync("/img.jpg", 640, 75, "image/webp", [1, 2, 3]);

        // Wait a tiny bit so the file write time is in the past
        await Task.Delay(50);

        var result = await shortTtlCache.GetAsync("/img.jpg", 640, 75, "image/webp");

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetOrCreateAsync_CacheMiss_CallsFactory()
    {
        var factoryCalled = false;
        var expectedData = new byte[] { 10, 20, 30 };

        var result = await _cache.GetOrCreateAsync("/img.jpg", 640, 75, "image/webp", () =>
        {
            factoryCalled = true;
            return Task.FromResult(expectedData);
        });

        factoryCalled.Should().BeTrue();
        result.Should().BeEquivalentTo(expectedData);
    }

    [Fact]
    public async Task GetOrCreateAsync_CacheHit_DoesNotCallFactory()
    {
        var data = new byte[] { 10, 20, 30 };
        await _cache.SetAsync("/img.jpg", 640, 75, "image/webp", data);

        var factoryCalled = false;

        var result = await _cache.GetOrCreateAsync("/img.jpg", 640, 75, "image/webp", () =>
        {
            factoryCalled = true;
            return Task.FromResult(new byte[] { 99 });
        });

        factoryCalled.Should().BeFalse();
        result.Should().BeEquivalentTo(data);
    }

    [Fact]
    public async Task GetOrCreateAsync_ConcurrentRequests_CallsFactoryOnce()
    {
        var factoryCallCount = 0;
        var data = new byte[] { 1, 2, 3 };

        var tasks = Enumerable.Range(0, 10).Select(_ =>
            _cache.GetOrCreateAsync("/concurrent.jpg", 640, 75, "image/webp", async () =>
            {
                Interlocked.Increment(ref factoryCallCount);
                await Task.Delay(100); // Simulate slow optimization
                return data;
            })
        ).ToArray();

        var results = await Task.WhenAll(tasks);

        factoryCallCount.Should().Be(1);
        results.Should().AllSatisfy(r => r.Should().BeEquivalentTo(data));
    }

    [Fact]
    public async Task SetAsync_CreatesDirectoryIfNotExists()
    {
        var nestedDir = Path.Combine(_cacheDir, "nested", "deep");
        var nestedCache = new ImageCache(new ImageOptimizationOptions
        {
            CacheDirectory = nestedDir
        });

        await nestedCache.SetAsync("/img.jpg", 640, 75, "image/webp", [1, 2, 3]);

        Directory.Exists(nestedDir).Should().BeTrue();
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Nextflow.Domain.Interfaces.Infrastructure.Services;
using Nextflow.Domain.Models;
using Nextflow.Infrastructure.Seeders.Demo;

namespace Nextflow.Tests.Integration;

/// <summary>Fonte de fotos dos testes: sem rede. Indisponível = como o Unsplash fora do ar.</summary>
public sealed class FakeDemoPhotoSource(bool available) : IDemoPhotoSource
{
    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0xFF, 0xD9];

    private int _calls;
    public int Calls => _calls;

    public Task<DemoPhoto?> Download(string photoId, CancellationToken ct)
    {
        Interlocked.Increment(ref _calls);
        return Task.FromResult(available ? new DemoPhoto(Jpeg, "image/jpeg") : null);
    }
}

[Collection(DemoSeedCollection.Name)]
public class DemoPhotosSeedTests(DemoSeedFixture fixture)
{
    private void Start(string connection, IFileStorage storage, IDemoPhotoSource source, bool demoEnabled = true)
        => fixture.StartApi(connection, demoEnabled, configureServices: services =>
        {
            services.AddSingleton(storage);
            services.AddSingleton(source);
        });

    private async Task<List<string>> ImageKeys(string connection)
    {
        await using var context = fixture.CreateContext(connection);
        return await context.Set<Product>().Where(p => p.ImageKey != null).Select(p => p.ImageKey!).OrderBy(k => k).ToListAsync();
    }

    [Fact]
    public async Task Should_UploadPhotosToProductImagesFolder_When_DemoSeedRuns()
    {
        var connection = await fixture.NewDatabaseAsync();
        var storage = new InMemoryFileStorage();

        Start(connection, storage, new FakeDemoPhotoSource(available: true));

        var keys = await ImageKeys(connection);
        keys.Count.ShouldBe(DemoPhotos.ByProductCode.Count);
        keys.ShouldAllBe(k => k.StartsWith("products/") && k.EndsWith(".jpg"));
        storage.Files.Keys.OrderBy(k => k).ShouldBe(keys);
    }

    [Fact]
    public async Task Should_NotDownloadAgain_When_SeedRunsTwice()
    {
        var connection = await fixture.NewDatabaseAsync();
        var storage = new InMemoryFileStorage();
        Start(connection, storage, new FakeDemoPhotoSource(available: true));
        var first = await ImageKeys(connection);

        var second = new FakeDemoPhotoSource(available: true);
        Start(connection, storage, second);

        second.Calls.ShouldBe(0);
        (await ImageKeys(connection)).ShouldBe(first);
        storage.Files.Count.ShouldBe(first.Count);
    }

    [Fact]
    public async Task Should_KeepSeedingWithoutPhotos_And_CompleteOnRestart_When_SourceIsDown()
    {
        var connection = await fixture.NewDatabaseAsync();
        var storage = new InMemoryFileStorage();

        Start(connection, storage, new FakeDemoPhotoSource(available: false));

        await using (var context = fixture.CreateContext(connection))
            (await context.Set<Product>().CountAsync()).ShouldBeGreaterThan(0);
        (await ImageKeys(connection)).ShouldBeEmpty();
        storage.Files.ShouldBeEmpty();

        Start(connection, storage, new FakeDemoPhotoSource(available: true));

        (await ImageKeys(connection)).Count.ShouldBe(DemoPhotos.ByProductCode.Count);
    }

    [Fact]
    public async Task Should_NotDownload_When_FlagDisabled()
    {
        var connection = await fixture.NewDatabaseAsync();
        var source = new FakeDemoPhotoSource(available: true);

        Start(connection, new InMemoryFileStorage(), source, demoEnabled: false);

        source.Calls.ShouldBe(0);
    }

    [Fact]
    public void Should_HavePhotoOnlyForCatalogProducts()
    {
        var codes = DemoCatalog.Products.Select(p => p.Code).ToHashSet();
        DemoPhotos.ByProductCode.Keys.ShouldAllBe(code => codes.Contains(code));
    }
}

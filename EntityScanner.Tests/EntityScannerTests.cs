using EntityScanner.Tests.DbContexts;
using Microsoft.EntityFrameworkCore;

namespace EntityScanner.Tests;

public class EntityScannerTests
{
    private LibraryDbContext _context = null;

    [SetUp]
    public async Task Setup()
    {
        // LibraryDbContextのセットアップ
        var options = new DbContextOptionsBuilder<LibraryDbContext>()
            .UseInMemoryDatabase(databaseName: "TestDatabase")
            .Options;

        // _contextを初期化
        _context = new LibraryDbContext(options);

        // 必要に応じてデータベースをクリーンアップ
        await _context.Database.EnsureDeletedAsync();
        await _context.Database.EnsureCreatedAsync();
    }

    [Test]
    public void LibraryDbContextが正しくセットアップされること()
    {
        Assert.That(_context, Is.Not.Null);
    }

    [TearDown]
    public async Task TearDown()
    {
        // データベースのクリーンアップ
        await _context.Database.EnsureDeletedAsync();

        // _contextを破棄
        await _context.DisposeAsync();
    }
}
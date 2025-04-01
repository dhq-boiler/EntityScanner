using EntityScanner.Tests.DbContexts;
using Microsoft.EntityFrameworkCore;

namespace EntityScanner.Tests;

public class EntityScannerTests
{
    private LibraryDbContext _context = null;

    [SetUp]
    public async Task Setup()
    {
        // LibraryDbContext�̃Z�b�g�A�b�v
        var options = new DbContextOptionsBuilder<LibraryDbContext>()
            .UseInMemoryDatabase(databaseName: "TestDatabase")
            .Options;

        // _context��������
        _context = new LibraryDbContext(options);

        // �K�v�ɉ����ăf�[�^�x�[�X���N���[���A�b�v
        await _context.Database.EnsureDeletedAsync();
        await _context.Database.EnsureCreatedAsync();
    }

    [Test]
    public void LibraryDbContext���������Z�b�g�A�b�v����邱��()
    {
        Assert.That(_context, Is.Not.Null);
    }

    [TearDown]
    public async Task TearDown()
    {
        // �f�[�^�x�[�X�̃N���[���A�b�v
        await _context.Database.EnsureDeletedAsync();

        // _context��j��
        await _context.DisposeAsync();
    }
}
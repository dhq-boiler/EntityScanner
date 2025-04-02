using System.Diagnostics;
using EntityScanner.Tests.DbContexts;
using EntityScanner.Tests.Entities;
using Microsoft.EntityFrameworkCore;

namespace EntityScanner.Tests;

[TestFixture]
[Category("Performance")]
public class EntityScannerPerformanceTests
{
    [SetUp]
    public void Setup()
    {
        // InMemoryデータベースを使用（性能テスト用）
        var dbName = $"PerformanceTestDb_{Guid.NewGuid()}";
        _options = new DbContextOptionsBuilder<LibraryDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        // EntityScannerを初期化
        _entityScanner = new EntityScanner();
    }

    [TearDown]
    public void TearDown()
    {
        _entityScanner.Clear();
    }

    private DbContextOptions<LibraryDbContext> _options;
    private EntityScanner _entityScanner;

    [Test]
    [Explicit("This test is resource-intensive and should be run manually")]
    public void ApplyToContext_With1000Categories_ShouldCompleteInReasonableTime()
    {
        // Arrange - 1000個のカテゴリを作成
        var categories = new List<Category>();
        for (var i = 1; i <= 1000; i++)
        {
            categories.Add(new Category
            {
                Id = i,
                Name = $"Category {i}",
                Description = $"Description for category {i}"
            });
        }

        foreach (var category in categories)
        {
            _entityScanner.RegisterEntity(category);
        }

        // Act
        var stopwatch = Stopwatch.StartNew();

        using (var context = new LibraryDbContext(_options))
        {
            _entityScanner.ApplyToContext(context);
            context.SaveChanges();
        }

        stopwatch.Stop();

        // Assert
        Console.WriteLine($"Time to apply 1000 categories: {stopwatch.ElapsedMilliseconds}ms");

        // 適切な時間内に完了すること（環境によって異なる）
        Assert.That(stopwatch.ElapsedMilliseconds, Is.LessThan(5000), "Should complete in less than 5 seconds");

        // すべてのエンティティが正しく保存されていることを確認
        using (var context = new LibraryDbContext(_options))
        {
            Assert.That(context.Categories.Count(), Is.EqualTo(1000));
        }
    }

    [Test]
    [Explicit("This test is resource-intensive and should be run manually")]
    public void ApplyToContext_WithComplexObjectGraph_ShouldHandleEfficiently()
    {
        // Arrange - 複雑なオブジェクトグラフを作成
        // 10の出版社、各出版社が10冊の本、各本が3人の著者を持つ

        var publishers = new List<Publisher>();
        var books = new List<Book>();
        var authors = new List<Author>();
        var bookAuthors = new List<BookAuthor>();

        // 10人の著者を作成
        for (var i = 1; i <= 10; i++)
        {
            authors.Add(new Author
            {
                Id = i,
                Name = $"Author {i}",
                Biography = $"Biography for author {i}"
            });
        }

        // 10の出版社を作成
        for (var i = 1; i <= 10; i++)
        {
            var publisher = new Publisher
            {
                Id = i,
                Name = $"Publisher {i}",
                Address = $"Address for publisher {i}"
            };
            publishers.Add(publisher);

            // 各出版社に10冊の本を作成
            for (var j = 1; j <= 10; j++)
            {
                var bookId = (i - 1) * 10 + j;
                var book = new Book
                {
                    Id = bookId,
                    Title = $"Book {bookId}",
                    Author = $"Main Author {bookId % 10 + 1}",
                    ISBN = $"ISBN-{bookId.ToString().PadLeft(10, '0')}",
                    PublicationYear = 2020 + bookId % 5,
                    Publisher = publisher,
                    PublisherId = publisher.Id,
                    CategoryId = 1 // すべて同じカテゴリに属する
                };
                books.Add(book);

                // 各本に3人の著者を関連付ける
                for (var k = 0; k < 3; k++)
                {
                    var authorIndex = (bookId + k) % 10;
                    var bookAuthor = new BookAuthor
                    {
                        Id = (bookId - 1) * 3 + k + 1,
                        Book = book,
                        BookId = book.Id,
                        Author = authors[authorIndex],
                        AuthorId = authors[authorIndex].Id,
                        Role = k == 0 ? "Main Author" : k == 1 ? "Co-Author" : "Editor"
                    };
                    bookAuthors.Add(bookAuthor);
                    book.Authors.Add(bookAuthor);
                    authors[authorIndex].Books.Add(bookAuthor);
                }

                publisher.PublishedBooks.Add(book);
            }
        }

        // カテゴリを1つ作成
        var category = new Category
        {
            Id = 1,
            Name = "General",
            Description = "General category for all books"
        };

        foreach (var book in books)
        {
            book.Category = category;
            category.Books.Add(book);
        }

        // EntityScannerにすべてのエンティティを登録
        _entityScanner.RegisterEntity(category);
        foreach (var publisher in publishers)
        {
            _entityScanner.RegisterEntity(publisher);
        }

        // Act
        var stopwatch = Stopwatch.StartNew();

        using (var context = new LibraryDbContext(_options))
        {
            _entityScanner.ApplyToContext(context);
            context.SaveChanges();
        }

        stopwatch.Stop();

        // Assert
        Console.WriteLine($"Time to apply complex object graph: {stopwatch.ElapsedMilliseconds}ms");
        Console.WriteLine(
            $"Number of entities - Category: 1, Publishers: {publishers.Count}, Books: {books.Count}, Authors: {authors.Count}, BookAuthors: {bookAuthors.Count}");

        // 適切な時間内に完了すること
        Assert.That(stopwatch.ElapsedMilliseconds, Is.LessThan(10000), "Should complete in less than 10 seconds");

        // すべてのエンティティが正しく保存されていることを確認
        using (var context = new LibraryDbContext(_options))
        {
            Assert.That(context.Categories.Count(), Is.EqualTo(1));
            Assert.That(context.Publishers.Count(), Is.EqualTo(10));
            Assert.That(context.Books.Count(), Is.EqualTo(100));
            Assert.That(context.Authors.Count(), Is.EqualTo(10));
            Assert.That(context.BookAuthors.Count(), Is.EqualTo(300));

            // 適当な本を取得して関連が正しいか確認
            var book42 = context.Books
                .Include(b => b.Publisher)
                .Include(b => b.Category)
                .Include(b => b.Authors)
                .ThenInclude(ba => ba.Author)
                .FirstOrDefault(b => b.Id == 42);

            Assert.That(book42, Is.Not.Null);
            Assert.That(book42.Title, Is.EqualTo("Book 42"));
            Assert.That(book42.Publisher.Name, Is.EqualTo("Publisher 5"));
            Assert.That(book42.Category.Name, Is.EqualTo("General"));
            Assert.That(book42.Authors.Count, Is.EqualTo(3));
        }
    }

    [Test]
    [Explicit("This test is resource-intensive and should be run manually")]
    public void ApplyToContext_WithNestedHierarchy_ShouldPerformEfficiently()
    {
        // Arrange - 深いネストされた階層を持つカテゴリ構造を作成

        // ルートカテゴリを作成
        var rootCategory = new Category
        {
            Id = 1,
            Name = "Root",
            Description = "Root category"
        };

        CreateCategoryHierarchy(rootCategory, 1, 5, 3);

        // EntityScannerにルートカテゴリを登録（階層全体を処理）
        _entityScanner.RegisterEntity(rootCategory);

        // Act
        var stopwatch = Stopwatch.StartNew();

        using (var context = new LibraryDbContext(_options))
        {
            _entityScanner.ApplyToContext(context);
            context.SaveChanges();
        }

        stopwatch.Stop();

        // Assert
        Console.WriteLine($"Time to apply nested hierarchy: {stopwatch.ElapsedMilliseconds}ms");

        // 適切な時間内に完了すること
        Assert.That(stopwatch.ElapsedMilliseconds, Is.LessThan(5000), "Should complete in less than 5 seconds");

        // すべてのカテゴリが正しく保存されていることを確認
        using (var context = new LibraryDbContext(_options))
        {
            // 階層構造の合計カテゴリ数を計算 (1 + 3 + 3*3 + 3*3*3 + 3*3*3*3 = 121)
            var expectedCount = 121;
            Assert.That(context.Categories.Count(), Is.EqualTo(expectedCount));

            // 正しい親子関係が設定されていることを確認
            var root = context.Categories
                .Include(c => c.SubCategories)
                .FirstOrDefault(c => c.Name == "Root");

            Assert.That(root, Is.Not.Null);
            Assert.That(root.SubCategories.Count, Is.EqualTo(3));

            // 最大深さのカテゴリが存在することを確認
            var leafCategory = context.Categories
                .FirstOrDefault(c => c.Name == "Category 1.3.3.3.3");

            Assert.That(leafCategory, Is.Not.Null);
            Assert.That(leafCategory.ParentCategoryId, Is.Not.Null);
        }
    }

    // 再帰的にカテゴリ階層を作成するヘルパーメソッド
    private void CreateCategoryHierarchy(Category parent, int level, int maxLevel, int childrenPerNode)
    {
        if (level >= maxLevel)
        {
            return;
        }

        for (var i = 1; i <= childrenPerNode; i++)
        {
            var path = $"{parent.Name.Replace("Root", "1")}.{i}";
            var child = new Category
            {
                Id = GenerateIdFromPath(path),
                Name = $"Category {path}",
                Description = $"Description for category {path}",
                ParentCategory = parent,
                ParentCategoryId = parent.Id
            };

            parent.SubCategories.Add(child);

            // 再帰的に子カテゴリを作成
            CreateCategoryHierarchy(child, level + 1, maxLevel, childrenPerNode);
        }
    }

    // パスから数値IDを生成するヘルパーメソッド
    private int GenerateIdFromPath(string path)
    {
        var parts = path.Split('.');
        var id = 0;

        for (var i = 0; i < parts.Length; i++)
        {
            id = id * 10 + int.Parse(parts[i]);
        }

        return id;
    }
}
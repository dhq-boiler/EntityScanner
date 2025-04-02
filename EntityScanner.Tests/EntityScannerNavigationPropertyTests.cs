using EntityScanner.Tests.DbContexts;
using EntityScanner.Tests.Entities;
using Microsoft.EntityFrameworkCore;

namespace EntityScanner.Tests;

[TestFixture]
public class EntityScannerNavigationPropertyTests
{
    [SetUp]
    public void Setup()
    {
        _entityScanner = new EntityScanner();
    }

    [TearDown]
    public void TearDown()
    {
        _entityScanner.Clear();
    }

    private EntityScanner _entityScanner;

    [Test]
    public void RegisterEntity_WithCategoryHierarchy_ShouldSetForeignKeys()
    {
        // Arrange
        // 階層構造を持つカテゴリを作成
        var rootCategory = new Category
        {
            Id = 1,
            Name = "Root Category",
            Description = "Root level category"
        };

        var childCategory = new Category
        {
            Id = 2,
            Name = "Child Category",
            Description = "Child level category",
            ParentCategory = rootCategory // 親カテゴリを設定
        };

        var grandchildCategory = new Category
        {
            Id = 3,
            Name = "Grandchild Category",
            Description = "Grandchild level category",
            ParentCategory = childCategory // 親カテゴリを設定（さらに深い階層）
        };

        // Act
        // エンティティをEntityScannerに登録
        _entityScanner.RegisterEntity(rootCategory);
        _entityScanner.RegisterEntity(childCategory);
        _entityScanner.RegisterEntity(grandchildCategory);

        // Assert
        // 外部キーが正しく設定されているか確認
        Assert.That(childCategory.ParentCategoryId, Is.EqualTo(1),
            "childCategoryのParentCategoryIdが正しく設定されていない");
        Assert.That(grandchildCategory.ParentCategoryId, Is.EqualTo(2),
            "grandchildCategoryのParentCategoryIdが正しく設定されていない");
    }

    [Test]
    public void GetSeedEntities_WithCategoryHierarchy_ShouldIncludeForeignKeys()
    {
        // Arrange
        // 階層構造を持つカテゴリを作成
        var rootCategory = new Category
        {
            Id = 1,
            Name = "Root Category",
            Description = "Root level category"
        };

        var childCategory = new Category
        {
            Id = 2,
            Name = "Child Category",
            Description = "Child level category",
            ParentCategory = rootCategory // 親カテゴリを設定
        };

        // 逆方向の関係も設定
        rootCategory.SubCategories.Add(childCategory);

        // エンティティを登録
        _entityScanner.RegisterEntity(rootCategory);

        // Act
        // シードエンティティを取得
        var seedEntities = _entityScanner.GetSeedEntities<Category>().ToList();

        // Assert
        // シードエンティティに外部キーが含まれているか確認
        Assert.That(seedEntities, Has.Count.EqualTo(2), "シードエンティティの数が正しくない");

        var rootSeed = seedEntities.FirstOrDefault(c => c.Id == 1);
        var childSeed = seedEntities.FirstOrDefault(c => c.Id == 2);

        Assert.That(rootSeed, Is.Not.Null, "rootCategoryのシードエンティティが見つからない");
        Assert.That(childSeed, Is.Not.Null, "childCategoryのシードエンティティが見つからない");

        Assert.That(rootSeed.ParentCategoryId, Is.Null, "rootCategoryのシードエンティティのParentCategoryIdはnullであるべき");
        Assert.That(childSeed.ParentCategoryId, Is.EqualTo(1), "childCategoryのシードエンティティのParentCategoryIdが正しくない");

        // ナビゲーションプロパティがシードエンティティに含まれていないことを確認
        Assert.That(rootSeed.ParentCategory, Is.Null, "rootCategoryのシードエンティティにParentCategoryが含まれている");
        Assert.That(rootSeed.SubCategories, Is.Empty, "rootCategoryのシードエンティティにSubCategoriesが含まれている");
    }

    [Test]
    public void RegisterEntity_WithBookAndRelatedEntities_ShouldSetAllForeignKeys()
    {
        // Arrange - 複雑な関係を持つエンティティを作成
        var category = new Category { Id = 1, Name = "Fiction", Description = "Fiction books" };
        var publisher = new Publisher { Id = 1, Name = "Test Publisher", Address = "123 Publisher St" };

        var book = new Book
        {
            Id = 1,
            Title = "Test Book",
            Author = "John Smith",
            ISBN = "1234567890",
            PublicationYear = 2023,
            Category = category, // ナビゲーションプロパティを設定
            Publisher = publisher // ナビゲーションプロパティを設定
        };

        // 逆方向の関係も設定
        category.Books.Add(book);
        publisher.PublishedBooks.Add(book);

        // Act
        _entityScanner.RegisterEntity(book);

        // Assert
        // 外部キーが正しく設定されているか確認
        Assert.That(book.CategoryId, Is.EqualTo(1), "bookのCategoryIdが正しく設定されていない");
        Assert.That(book.PublisherId, Is.EqualTo(1), "bookのPublisherIdが正しく設定されていない");

        // GetSeedEntitiesでも確認
        var seedEntities = _entityScanner.GetSeedEntities<Book>().ToList();
        Assert.That(seedEntities, Has.Count.EqualTo(1), "シードエンティティの数が正しくない");
        var seedEntity = seedEntities[0];
        Assert.That(seedEntity.CategoryId, Is.EqualTo(1), "シードエンティティのCategoryIdが正しく設定されていない");
        Assert.That(seedEntity.PublisherId, Is.EqualTo(1), "シードエンティティのPublisherIdが正しく設定されていない");
    }

    [Test]
    public void RegisterEntity_WithManyToManyRelationship_ShouldSetForeignKeys()
    {
        // Arrange - 多対多の関係を持つエンティティを作成
        var book = new Book
        {
            Id = 1,
            Title = "Programming C#",
            Author = "Primary Author", // Author字段
            ISBN = "9781234567890",
            PublicationYear = 2023
        };

        var author1 = new Author
        {
            Id = 1,
            Name = "John Smith",
            Biography = "Experienced C# developer"
        };

        var author2 = new Author
        {
            Id = 2,
            Name = "Jane Doe",
            Biography = "Technical writer"
        };

        // 中間テーブルのエンティティを作成
        var bookAuthor1 = new BookAuthor
        {
            Id = 1,
            Book = book,
            Author = author1,
            Role = "Primary Author"
        };

        var bookAuthor2 = new BookAuthor
        {
            Id = 2,
            Book = book,
            Author = author2,
            Role = "Co-Author"
        };

        // 逆方向の関係も設定
        book.Authors.Add(bookAuthor1);
        book.Authors.Add(bookAuthor2);
        author1.Books.Add(bookAuthor1);
        author2.Books.Add(bookAuthor2);

        // Act
        _entityScanner.RegisterEntity(book);

        // Assert
        // 外部キーが正しく設定されているか確認
        Assert.That(bookAuthor1.BookId, Is.EqualTo(1), "bookAuthor1のBookIdが正しく設定されていない");
        Assert.That(bookAuthor1.AuthorId, Is.EqualTo(1), "bookAuthor1のAuthorIdが正しく設定されていない");
        Assert.That(bookAuthor2.BookId, Is.EqualTo(1), "bookAuthor2のBookIdが正しく設定されていない");
        Assert.That(bookAuthor2.AuthorId, Is.EqualTo(2), "bookAuthor2のAuthorIdが正しく設定されていない");

        // GetSeedEntitiesでも確認
        var seedEntities = _entityScanner.GetSeedEntities<BookAuthor>().ToList();
        Assert.That(seedEntities, Has.Count.EqualTo(2), "シードエンティティの数が正しくない");

        var seedEntity1 = seedEntities.FirstOrDefault(ba => ba.Id == 1);
        var seedEntity2 = seedEntities.FirstOrDefault(ba => ba.Id == 2);

        Assert.That(seedEntity1, Is.Not.Null, "bookAuthor1のシードエンティティが見つからない");
        Assert.That(seedEntity2, Is.Not.Null, "bookAuthor2のシードエンティティが見つからない");

        Assert.That(seedEntity1.BookId, Is.EqualTo(1), "シードエンティティ1のBookIdが正しく設定されていない");
        Assert.That(seedEntity1.AuthorId, Is.EqualTo(1), "シードエンティティ1のAuthorIdが正しく設定されていない");
        Assert.That(seedEntity2.BookId, Is.EqualTo(1), "シードエンティティ2のBookIdが正しく設定されていない");
        Assert.That(seedEntity2.AuthorId, Is.EqualTo(2), "シードエンティティ2のAuthorIdが正しく設定されていない");
    }

    [Test]
    public void ApplyToContext_WithLibraryEntities_ShouldSaveCorrectForeignKeys()
    {
        // DBコンテキストの設定
        var connectionString = $"Data Source=NavigationPropertyTest_{Guid.NewGuid()}.db";
        var options = new DbContextOptionsBuilder<LibraryDbContext>()
            .UseSqlite(connectionString)
            .EnableSensitiveDataLogging()
            .Options;

        try
        {
            // Arrange - 図書館モデルを使用した複雑な関係を作成
            var library = new Library { Id = 1, Name = "Main Library", Address = "123 Main St" };
            var shelf = new Shelf { Id = 1, ShelfCode = "A1", Location = "First Floor", Library = library };

            var category = new Category { Id = 1, Name = "Computer Science", Description = "Technical books" };
            var publisher = new Publisher { Id = 1, Name = "Tech Books Inc.", Address = "456 Tech St" };

            var book = new Book
            {
                Id = 1,
                Title = "Entity Framework Core in Action",
                Author = "Jon Smith",
                ISBN = "9781617294563",
                PublicationYear = 2018,
                Category = category,
                Publisher = publisher
            };

            var author = new Author
            {
                Id = 1,
                Name = "Jon Smith",
                Biography = "Software engineer specializing in .NET"
            };

            var bookAuthor = new BookAuthor
            {
                Id = 1,
                Book = book,
                Author = author,
                Role = "Main Author"
            };

            var bookInventory = new BookInventory
            {
                Id = 1,
                Book = book,
                Shelf = shelf,
                CopyNumber = "CS001-001",
                Status = BookInventory.StatusEnum.Available
            };

            // 関係を設定
            library.Shelves.Add(shelf);
            shelf.BookInventories.Add(bookInventory);
            book.Authors.Add(bookAuthor);
            author.Books.Add(bookAuthor);
            category.Books.Add(book);
            publisher.PublishedBooks.Add(book);

            // エンティティを登録
            _entityScanner.RegisterEntity(library);

            // データベースを作成
            using (var context = new LibraryDbContext(options))
            {
                context.Database.EnsureCreated();
            }

            // Act
            using (var context = new LibraryDbContext(options))
            {
                _entityScanner.ApplyToContext(context);
                context.SaveChanges();
            }

            // Assert
            using (var context = new LibraryDbContext(options))
            {
                // 保存されたデータを取得して確認
                var savedLibrary = context.Libraries
                    .Include(l => l.Shelves)
                    .ThenInclude(s => s.BookInventories)
                    .ThenInclude(bi => bi.Book)
                    .FirstOrDefault();

                Assert.That(savedLibrary, Is.Not.Null, "図書館が保存されていない");
                Assert.That(savedLibrary.Shelves, Has.Count.EqualTo(1), "図書館の棚の数が正しくない");

                var savedShelf = savedLibrary.Shelves.FirstOrDefault();
                Assert.That(savedShelf, Is.Not.Null, "棚が保存されていない");
                Assert.That(savedShelf.LibraryId, Is.EqualTo(1), "棚のLibraryIdが正しくない");
                Assert.That(savedShelf.BookInventories, Has.Count.EqualTo(1), "棚の本の在庫数が正しくない");

                var savedInventory = savedShelf.BookInventories.FirstOrDefault();
                Assert.That(savedInventory, Is.Not.Null, "本の在庫が保存されていない");
                Assert.That(savedInventory.ShelfId, Is.EqualTo(1), "本の在庫のShelfIdが正しくない");
                Assert.That(savedInventory.BookId, Is.EqualTo(1), "本の在庫のBookIdが正しくない");

                // BookAuthorの関係を確認
                var savedBookAuthor = context.BookAuthors
                    .Include(ba => ba.Book)
                    .Include(ba => ba.Author)
                    .FirstOrDefault();

                Assert.That(savedBookAuthor, Is.Not.Null, "BookAuthorが保存されていない");
                Assert.That(savedBookAuthor.BookId, Is.EqualTo(1), "BookAuthorのBookIdが正しくない");
                Assert.That(savedBookAuthor.AuthorId, Is.EqualTo(1), "BookAuthorのAuthorIdが正しくない");
            }
        }
        finally
        {
            // 後片付け - SQLiteファイルを削除
            var dbFileName = connectionString.Replace("Data Source=", "");
            if (File.Exists(dbFileName))
            {
                try
                {
                    File.Delete(dbFileName);
                }
                catch
                {
                    // ファイル削除に失敗しても無視
                }
            }
        }
    }
}
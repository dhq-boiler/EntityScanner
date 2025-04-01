using EntityScanner.Tests.DbContexts;
using EntityScanner.Tests.Entities;
using Microsoft.EntityFrameworkCore;

namespace EntityScanner.Tests;

[TestFixture]
public class EntityScannerTests
{
    [SetUp]
    public void Setup()
    {
        // SQLiteを使用するための設定
        var connectionString = $"Data Source=LibraryTestDb_{Guid.NewGuid()}.db";
        _options = new DbContextOptionsBuilder<LibraryDbContext>()
            .UseSqlite(connectionString)  // InMemoryからSQLiteに変更
            .EnableSensitiveDataLogging()
            .Options;

        // 接続文字列を保存して後でファイルを削除できるようにする
        _connectionString = connectionString;

        // Initialize the EntityScanner
        _entityScanner = new EntityScanner();
    }

    [TearDown]
    public void TearDown()
    {
        _entityScanner.Clear();
    }

    private DbContextOptions<LibraryDbContext> _options = null;
    private string _connectionString;  // SQLiteファイル名を保持するための変数を追加
    private EntityScanner _entityScanner = null;

    [Test]
    public void RegisterEntity_ShouldAddEntityToCollection()
    {
        // Arrange
        var category = new Category { Id = 1, Name = "Fiction" };

        // Act
        _entityScanner.RegisterEntity(category);
        var registeredCategories = _entityScanner.GetEntities<Category>().ToList();

        // Assert
        Assert.That(registeredCategories, Has.Count.EqualTo(1));
        Assert.That(registeredCategories[0].Name, Is.EqualTo("Fiction"));
    }

    [Test]
    public void RegisterEntity_WithOneToManyRelationship_ShouldSetForeignKeys()
    {
        // Arrange
        var category = new Category { Id = 1, Name = "Fiction" };
        var book = new Book
        {
            Id = 1,
            Title = "Test Book",
            Category = category
            // Note: CategoryId is not explicitly set
        };

        // Act
        _entityScanner.RegisterEntity(book);

        // Assert
        Assert.That(book.CategoryId, Is.EqualTo(1), "Foreign key should be automatically set");

        var categories = _entityScanner.GetEntities<Category>().ToList();
        Assert.That(categories, Has.Count.EqualTo(1), "Related entity should be automatically registered");
    }

    [Test]
    public void RegisterEntity_WithOneToOneRelationship_ShouldSetForeignKeys()
    {
        // Arrange
        var member = new Member { Id = 1, Name = "John Doe", Email = "john@example.com" };
        var profile = new MemberProfile
        {
            Id = 1,
            PhoneNumber = "123-456-7890",
            Member = member
            // Note: MemberId is not explicitly set
        };

        // Act
        _entityScanner.RegisterEntity(profile);

        // Assert
        Assert.That(profile.MemberId, Is.EqualTo(1), "Foreign key should be automatically set");

        var members = _entityScanner.GetEntities<Member>().ToList();
        Assert.That(members, Has.Count.EqualTo(1), "Related entity should be automatically registered");
    }

    [Test]
    public void RegisterEntity_WithBidirectionalRelationship_ShouldSetForeignKeysAndNavigationProperties()
    {
        // Arrange
        var library = new Library { Id = 1, Name = "Main Library" };
        var shelf = new Shelf { Id = 1, ShelfCode = "A1", Library = library };

        // Add shelf to library's collection
        library.Shelves.Add(shelf);

        // Act
        _entityScanner.RegisterEntity(library);

        // Assert
        Assert.That(shelf.LibraryId, Is.EqualTo(1), "Foreign key should be automatically set");

        var shelves = _entityScanner.GetEntities<Shelf>().ToList();
        Assert.That(shelves, Has.Count.EqualTo(1), "Related entity should be automatically registered");
    }

    [Test]
    public void RegisterEntity_WithManyToManyRelationship_ShouldSetForeignKeys()
    {
        // Arrange
        var book = new Book { Id = 1, Title = "Programming C#" };
        var author = new Author { Id = 1, Name = "John Sharp" };
        var bookAuthor = new BookAuthor
        {
            Id = 1,
            Book = book,
            Author = author,
            Role = "Main Author"
        };

        book.Authors.Add(bookAuthor);
        author.Books.Add(bookAuthor);

        // Act
        _entityScanner.RegisterEntity(book);

        // Assert
        Assert.That(bookAuthor.BookId, Is.EqualTo(1), "BookId foreign key should be automatically set");
        Assert.That(bookAuthor.AuthorId, Is.EqualTo(1), "AuthorId foreign key should be automatically set");

        var authors = _entityScanner.GetEntities<Author>().ToList();
        Assert.That(authors, Has.Count.EqualTo(1), "Related author entity should be registered");

        var bookAuthors = _entityScanner.GetEntities<BookAuthor>().ToList();
        Assert.That(bookAuthors, Has.Count.EqualTo(1), "Join entity should be registered");
    }

    [Test]
    public void RegisterEntity_WithHierarchicalRelationship_ShouldSetForeignKeys()
    {
        // Arrange
        var parentCategory = new Category { Id = 1, Name = "Fiction" };
        var childCategory = new Category
        {
            Id = 2,
            Name = "Science Fiction",
            ParentCategory = parentCategory
        };

        parentCategory.SubCategories.Add(childCategory);

        // Act
        _entityScanner.RegisterEntity(parentCategory);

        // Assert
        Assert.That(childCategory.ParentCategoryId, Is.EqualTo(1), "Foreign key should be automatically set");

        var categories = _entityScanner.GetEntities<Category>().ToList();
        Assert.That(categories, Has.Count.EqualTo(2), "Both parent and child entities should be registered");
    }

    [Test]
    public void ApplyToContext_ShouldAddEntitiesToDbContext()
    {
        // Arrange
        var category = new Category { Id = 1, Name = "Fiction", Description = "Fiction books and novels" };
        var book = new Book
        {
            Id = 1,
            Title = "Test Book",
            Author = "Test Author",
            ISBN = "1234567890",
            PublicationYear = 2022,
            Category = category,
            PublisherId = 1
        };

        _entityScanner.RegisterEntity(book);

        // Act
        using (var context = new LibraryDbContext(_options))
        {
            _entityScanner.ApplyToContext(context);
            context.SaveChanges();
        }

        // Assert
        using (var context = new LibraryDbContext(_options))
        {
            Assert.That(context.Books.Count(), Is.EqualTo(1), "Book should be saved to database");
            Assert.That(context.Categories.Count(), Is.EqualTo(1), "Category should be saved to database");

            var savedBook = context.Books.Include(b => b.Category).FirstOrDefault();
            Assert.That(savedBook, Is.Not.Null);
            Assert.That(savedBook.CategoryId, Is.EqualTo(1));
            Assert.That(savedBook.Category.Name, Is.EqualTo("Fiction"));
        }
    }

    [Test]
    public void GetSeedData_ShouldReturnObjectsWithoutNavigationProperties()
    {
        // Arrange
        var category = new Category { Id = 1, Name = "Fiction", Description = "Fiction books" };
        var book = new Book
        {
            Id = 1,
            Title = "Test Book",
            Author = "Test Author",
            ISBN = "1234567890",
            PublicationYear = 2022,
            Category = category,
            PublisherId = 1
        };

        _entityScanner.RegisterEntity(book);

        // Act
        var seedData = _entityScanner.GetSeedData<Book>().ToList();

        // Assert
        Assert.That(seedData, Has.Count.EqualTo(1));

        // Convert to dictionary to check properties
        var bookDict = seedData[0] as IDictionary<string, object>;
        Assert.That(bookDict, Is.Not.Null);

        // Check that basic properties are included
        Assert.That(bookDict["Id"], Is.EqualTo(1));
        Assert.That(bookDict["Title"], Is.EqualTo("Test Book"));
        Assert.That(bookDict["CategoryId"], Is.EqualTo(1));

        // Check that navigation properties are not included
        Assert.That(bookDict.ContainsKey("Category"), Is.False, "Navigation property should not be included");
        Assert.That(bookDict.ContainsKey("BorrowRecords"), Is.False,
            "Collection navigation property should not be included");
    }

    [Test]
    public void ComplexScenario_LibraryWithMultipleEntities_ShouldMaintainAllRelationships()
    {
        // Arrange - Create a complex object graph
        var library = new Library { Id = 1, Name = "Central Library", Address = "123 Main St" };
        var shelf = new Shelf { Id = 1, ShelfCode = "A1", Location = "First Floor", Library = library };

        var category = new Category
        { Id = 1, Name = "Computer Science", Description = "Books about programming and computer science" };
        var publisher = new Publisher
        { Id = 1, Name = "Tech Books Inc.", Address = "456 Tech Boulevard, Silicon Valley, CA" };
        var book = new Book
        {
            Id = 1,
            Title = "Entity Framework Core in Action",
            ISBN = "9781617294563",
            PublicationYear = 2018,
            Category = category,
            Publisher = publisher,
            Author = "Jon Smith" // Adding the required Author property
        };

        var author = new Author
        {
            Id = 1,
            Name = "Jon Smith",
            Biography = "Software engineer and technical author specializing in .NET technologies."
        };
        var bookAuthor = new BookAuthor { Id = 1, Book = book, Author = author, Role = "Main Author" };

        var bookInventory = new BookInventory
        {
            Id = 1,
            Book = book,
            Shelf = shelf,
            CopyNumber = "CS001-001",
            Status = BookInventory.StatusEnum.Available
        };

        // Setup relationships
        library.Shelves.Add(shelf);
        shelf.BookInventories.Add(bookInventory);

        book.Authors.Add(bookAuthor);
        author.Books.Add(bookAuthor);

        category.Books.Add(book);
        publisher.PublishedBooks.Add(book);

        // このテスト用の一意のSQLiteファイルを使用
        var testDbPath = $"Data Source=ComplexScenarioTestDb_{Guid.NewGuid()}.db";
        var options = new DbContextOptionsBuilder<LibraryDbContext>()
            .UseSqlite(testDbPath)
            .EnableSensitiveDataLogging()
            .Options;

        try
        {
            // Act - Register the root entity
            _entityScanner.RegisterEntity(library);

            // データベースとスキーマを作成
            using (var context = new LibraryDbContext(options))
            {
                // テーブルが存在することを確認
                context.Database.EnsureCreated();
            }

            // エンティティをデータベースに保存
            using (var context = new LibraryDbContext(options))
            {
                _entityScanner.ApplyToContext(context);

                // データベースに変更を保存
                try
                {
                    context.SaveChanges();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"データ保存エラー: {ex.Message}");
                    Console.WriteLine($"内部例外: {ex.InnerException?.Message}");
                    throw;
                }
            }

            // Assert - Verify relationships are maintained
            using (var context = new LibraryDbContext(options))
            {
                // Check entity counts
                Assert.That(context.Libraries.Count(), Is.EqualTo(1), "図書館が1件存在するべき");
                Assert.That(context.Shelves.Count(), Is.EqualTo(1), "本棚が1件存在するべき");
                Assert.That(context.Books.Count(), Is.EqualTo(1), "書籍が1件存在するべき");
                Assert.That(context.Categories.Count(), Is.EqualTo(1), "カテゴリが1件存在するべき");
                Assert.That(context.Publishers.Count(), Is.EqualTo(1), "出版社が1件存在するべき");
                Assert.That(context.Authors.Count(), Is.EqualTo(1), "著者が1件存在するべき");
                Assert.That(context.BookAuthors.Count(), Is.EqualTo(1), "書籍著者関連が1件存在するべき");
                Assert.That(context.BookInventories.Count(), Is.EqualTo(1), "書籍在庫が1件存在するべき");

                // Verify relationships by loading with Include
                var loadedLibrary = context.Libraries
                    .Include(l => l.Shelves)
                    .ThenInclude(s => s.BookInventories)
                    .ThenInclude(bi => bi.Book)
                    .ThenInclude(b => b.Category)
                    .FirstOrDefault();

                Assert.That(loadedLibrary, Is.Not.Null, "図書館が取得できるべき");

                if (loadedLibrary != null)
                {
                    Assert.That(loadedLibrary.Shelves, Has.Count.EqualTo(1), "図書館には1件の本棚があるべき");

                    if (loadedLibrary.Shelves.Count > 0)
                    {
                        Assert.That(loadedLibrary.Shelves[0].BookInventories, Has.Count.EqualTo(1), "本棚には1件の書籍在庫があるべき");

                        if (loadedLibrary.Shelves[0].BookInventories.Count > 0)
                        {
                            Assert.That(loadedLibrary.Shelves[0].BookInventories[0].Book.Title,
                                Is.EqualTo("Entity Framework Core in Action"), "書籍タイトルが正しいべき");
                            Assert.That(loadedLibrary.Shelves[0].BookInventories[0].Book.Category.Name,
                                Is.EqualTo("Computer Science"), "書籍カテゴリが正しいべき");
                        }
                    }
                }

                // Check book-author relationship
                var loadedBook = context.Books
                    .Include(b => b.Authors)
                    .ThenInclude(ba => ba.Author)
                    .FirstOrDefault();

                Assert.That(loadedBook, Is.Not.Null, "書籍が取得できるべき");

                if (loadedBook != null)
                {
                    Assert.That(loadedBook.Authors, Has.Count.EqualTo(1), "書籍には1人の著者が関連付けられているべき");

                    if (loadedBook.Authors.Count > 0)
                    {
                        Assert.That(loadedBook.Authors[0].Author.Name, Is.EqualTo("Jon Smith"), "著者名が正しいべき");
                    }
                }
            }
        }
        finally
        {
            // テスト後にSQLiteファイルを削除
            var dbFileName = testDbPath.Replace("Data Source=", "");
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

    // このテストは、GetSeedDataメソッドの動作を検証します（ApplyToModelBuilderは使用しません）
    [Test]
    public void GetSeedData_ShouldReturnCorrectData()
    {
        // 準備
        var category = new Category { Id = 1, Name = "小説", Description = "フィクション書籍" };
        var book = new Book
        {
            Id = 1,
            Title = "テスト本",
            Author = "テスト著者",
            ISBN = "1234567890",
            PublicationYear = 2022,
            Category = category,
            CategoryId = 1,
            PublisherId = 1
        };

        // エンティティを登録
        _entityScanner.RegisterEntity(category);
        _entityScanner.RegisterEntity(book);

        // GetSeedDataメソッドを使用してシードデータを取得
        var bookSeedData = _entityScanner.GetSeedData<Book>().ToList();
        var categorySeedData = _entityScanner.GetSeedData<Category>().ToList();

        // 検証：必要なプロパティがすべて含まれているか
        Assert.That(bookSeedData, Has.Count.EqualTo(1), "Book seed data should have 1 item");
        Assert.That(categorySeedData, Has.Count.EqualTo(1), "Category seed data should have 1 item");

        // BookのシードデータをDictionaryとして取得して内容を検証
        var bookSeed = bookSeedData[0] as IDictionary<string, object>;
        Assert.That(bookSeed, Is.Not.Null, "Book seed data should be convertible to IDictionary");
        Assert.That(bookSeed.ContainsKey("Id"), Is.True, "Book seed data should contain Id");
        Assert.That(bookSeed["Id"], Is.EqualTo(1), "Book Id should be 1");
        Assert.That(bookSeed.ContainsKey("Title"), Is.True, "Book seed data should contain Title");
        Assert.That(bookSeed["Title"], Is.EqualTo("テスト本"), "Book Title should be テスト本");
        Assert.That(bookSeed.ContainsKey("CategoryId"), Is.True, "Book seed data should contain CategoryId");
        Assert.That(bookSeed["CategoryId"], Is.EqualTo(1), "Book CategoryId should be 1");

        // カテゴリのシードデータも同様に検証
        var categorySeed = categorySeedData[0] as IDictionary<string, object>;
        Assert.That(categorySeed, Is.Not.Null, "Category seed data should be convertible to IDictionary");
        Assert.That(categorySeed.ContainsKey("Id"), Is.True, "Category seed data should contain Id");
        Assert.That(categorySeed["Id"], Is.EqualTo(1), "Category Id should be 1");
        Assert.That(categorySeed.ContainsKey("Name"), Is.True, "Category seed data should contain Name");
        Assert.That(categorySeed["Name"], Is.EqualTo("小説"), "Category Name should be 小説");
    }
}
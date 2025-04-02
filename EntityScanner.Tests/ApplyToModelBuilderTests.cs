using EntityScanner.Tests.DbContexts;
using EntityScanner.Tests.Entities;
using Microsoft.EntityFrameworkCore;

namespace EntityScanner.Tests;

[TestFixture]
public class ApplyToModelBuilderTests
{
    [SetUp]
    public void Setup()
    {
        // SQLiteを使用するためのユニークなDB接続文字列を用意
        var connectionString = $"Data Source=ApplyToModelBuilderTests_{Guid.NewGuid()}.db";
        _options = new DbContextOptionsBuilder<LibraryDbContext>()
            .UseSqlite(connectionString, options =>
            {
                // SQLite接続オプションを調整
                options.MigrationsAssembly(typeof(LibraryDbContext).Assembly.FullName);
                // コマンドタイムアウトを延長
                options.CommandTimeout(60);
            })
            .EnableSensitiveDataLogging() // デバッグのためにクエリログを詳細に
            .EnableDetailedErrors() // 詳細なエラーを有効に
            .Options;

        // 接続文字列を保存
        _connectionString = connectionString;

        // EntityScannerを初期化
        _entityScanner = new EntityScanner();
    }

    [TearDown]
    public void TearDown()
    {
        _entityScanner.Clear();

        // SQLiteファイルを削除
        var dbFileName = _connectionString.Replace("Data Source=", "");
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

    private DbContextOptions<LibraryDbContext> _options;
    private string _connectionString;
    private EntityScanner _entityScanner;

    [Test]
    public async Task ApplyAToModelBuilder_DuplicateRecords_ShouldUpdate()
    {
        var category = new Category { Id = 1, Name = "Fiction", Description = "Fiction books" };
        var category2 = new Category { Id = 1, Name = "Non-Fiction", Description = "Non-fiction books" };

        _entityScanner.DuplicateBehavior = DuplicateEntityBehavior.Update;

        _entityScanner.RegisterEntity(category);
        _entityScanner.RegisterEntity(category2);

        using (var context = new TestLibraryDbContext(_options, _entityScanner))
        {
            await context.Database.EnsureCreatedAsync();

            var categories = context.Categories.ToList();
            Assert.That(categories, Has.Count.EqualTo(1), "Category should be seeded");
            Assert.That(categories.Any(c => c.Name == "Non-Fiction"), Is.True, "Non-Fiction category should be seeded");
        }
    }
}
//    [Test]
//    public async Task ApplyToModelBuilder_WithSimpleEntity_ShouldApplySeedData()
//    {
//        // Arrange
//        var category = new Category { Id = 1, Name = "Fiction", Description = "Fiction books" };
//        _entityScanner.RegisterEntity(category);

//        // Act & Assert
//        using (var context = await CreateContextWithModelBuilder())
//        {
//            var categories = context.Categories.ToList();
//            Assert.That(categories, Has.Count.EqualTo(1), "Category should be seeded");
//            Assert.That(categories[0].Name, Is.EqualTo("Fiction"), "Category name should match");
//            Assert.That(categories[0].Description, Is.EqualTo("Fiction books"), "Category description should match");
//        }
//    }

//    [Test]
//    public async Task ApplyToModelBuilder_WithMultipleEntities_ShouldApplySeedData()
//    {
//        // Arrange
//        var category1 = new Category { Id = 1, Name = "Fiction", Description = "Fiction books" };
//        var category2 = new Category { Id = 2, Name = "Non-Fiction", Description = "Non-fiction books" };

//        _entityScanner.RegisterEntity(category1);
//        _entityScanner.RegisterEntity(category2);

//        // Act & Assert
//        using (var context = await CreateContextWithModelBuilder())
//        {
//            var categories = context.Categories.ToList();
//            Assert.That(categories, Has.Count.EqualTo(2), "Both categories should be seeded");
//            Assert.That(categories.Any(c => c.Name == "Fiction"), Is.True, "Fiction category should be seeded");
//            Assert.That(categories.Any(c => c.Name == "Non-Fiction"), Is.True, "Non-Fiction category should be seeded");
//        }
//    }

//    [Test]
//    public async Task ApplyToModelBuilder_WithRelatedEntities_ShouldApplySeedData()
//    {
//        // Arrange
//        var category = new Category { Id = 1, Name = "Programming", Description = "Programming books" };
//        var publisher = new Publisher { Id = 1, Name = "Tech Books", Address = "123 Tech St" };

//        var book = new Book
//        {
//            Id = 1,
//            Title = "C# in Depth",
//            Author = "Jon Skeet",
//            ISBN = "978-1617294532",
//            PublicationYear = 2019,
//            Category = category,
//            Publisher = publisher
//        };

//        _entityScanner.RegisterEntity(book);

//        // Act & Assert
//        using (var context = await CreateContextWithModelBuilder())
//        {
//            var books = context.Books.Include(b => b.Category).Include(b => b.Publisher).ToList();
//            Assert.That(books, Has.Count.EqualTo(1), "Book should be seeded");

//            var loadedBook = books.First();
//            Assert.That(loadedBook.Title, Is.EqualTo("C# in Depth"), "Book title should match");
//            Assert.That(loadedBook.CategoryId, Is.EqualTo(1), "CategoryId should be set correctly");
//            Assert.That(loadedBook.PublisherId, Is.EqualTo(1), "PublisherId should be set correctly");

//            var categories = context.Categories.ToList();
//            Assert.That(categories, Has.Count.EqualTo(1), "Category should be seeded");

//            var publishers = context.Publishers.ToList();
//            Assert.That(publishers, Has.Count.EqualTo(1), "Publisher should be seeded");
//        }
//    }

//    [Test]
//    public async Task ApplyToModelBuilder_WithManyToManyRelationship_ShouldApplySeedData()
//    {
//        // Arrange
//        var author = new Author { Id = 1, Name = "John Smith", Biography = "Famous author" };
//        var category = new Category { Id = 1, Name = "Fiction", Description = "Fiction books" };
//        var publisher = new Publisher { Id = 1, Name = "Big Publisher", Address = "Publisher St" };

//        var book = new Book
//        {
//            Id = 1,
//            Title = "Sample Book",
//            Author = "John Smith",
//            ISBN = "123456789",
//            PublicationYear = 2022,
//            Category = category,
//            Publisher = publisher
//        };

//        var bookAuthor = new BookAuthor
//        {
//            Id = 1,
//            Book = book,
//            Author = author,
//            Role = "Main Author"
//        };

//        book.Authors.Add(bookAuthor);
//        author.Books.Add(bookAuthor);

//        _entityScanner.RegisterEntity(book);

//        // Act & Assert
//        using (var context = await CreateContextWithModelBuilder())
//        {
//            var books = context.Books.ToList();
//            Assert.That(books, Has.Count.EqualTo(1), "Book should be seeded");

//            var authors = context.Authors.ToList();
//            Assert.That(authors, Has.Count.EqualTo(1), "Author should be seeded");

//            var bookAuthors = context.BookAuthors.ToList();
//            Assert.That(bookAuthors, Has.Count.EqualTo(1), "BookAuthor join entity should be seeded");
//            Assert.That(bookAuthors[0].BookId, Is.EqualTo(1), "BookId should be set correctly");
//            Assert.That(bookAuthors[0].AuthorId, Is.EqualTo(1), "AuthorId should be set correctly");
//        }
//    }

//    [Test]
//    public async Task ApplyToModelBuilder_WithCircularReferences_ShouldApplySeedData()
//    {
//        // Arrange - 循環参照を持つデータの準備
//        var member = new Member
//        {
//            Id = 1,
//            Name = "John Doe",
//            Email = "john@example.com",
//            RegistrationDate = DateTime.Now.Date
//        };

//        var profile = new MemberProfile
//        {
//            Id = 1,
//            PhoneNumber = "123-456-7890",
//            Address = "123 Main St",
//            Member = member,
//            MemberId = 1
//        };

//        member.Profile = profile;

//        _entityScanner.RegisterEntity(member);

//        // Act & Assert
//        using (var context = await CreateContextWithModelBuilder())
//        {
//            var members = context.Members.ToList();
//            Assert.That(members, Has.Count.EqualTo(1), "Member should be seeded");

//            var profiles = context.MemberProfiles.ToList();
//            Assert.That(profiles, Has.Count.EqualTo(1), "Profile should be seeded");
//            Assert.That(profiles[0].MemberId, Is.EqualTo(1), "MemberId should be set correctly");
//        }
//    }

//    [Test]
//    public async Task ApplyToModelBuilder_WithHierarchicalData_ShouldApplySeedData()
//    {
//        // テスト用データ作成前に明示的にEntityScannerをクリア
//        _entityScanner.Clear();

//        // Arrange - 階層構造を持つデータの準備
//        var rootCategory = new Category { Id = 1, Name = "Root", Description = "Root category" };
//        var childCategory = new Category
//        {
//            Id = 2,
//            Name = "Child",
//            Description = "Child category",
//            ParentCategory = rootCategory,
//            ParentCategoryId = 1
//        };

//        rootCategory.SubCategories.Add(childCategory);

//        // 明示的に外部キーを設定（念のため）
//        childCategory.ParentCategoryId = rootCategory.Id;

//        _entityScanner.RegisterEntity(rootCategory);

//        // Act & Assert - トランザクションを使用してテストを安定化
//        using (var context = await CreateContextWithModelBuilder())
//        using (var transaction = context.Database.BeginTransaction())
//        {
//            try
//            {
//                var categories = context.Categories
//                    .AsNoTracking() // トラッキングを無効化して確実に新しいデータを取得
//                    .OrderBy(c => c.Id) // 順序を明示的に指定
//                    .ToList();

//                Assert.That(categories, Has.Count.EqualTo(2), "Both categories should be seeded");

//                var rootFromDb = categories.FirstOrDefault(c => c.Id == 1);
//                var childFromDb = categories.FirstOrDefault(c => c.Id == 2);

//                Assert.That(rootFromDb, Is.Not.Null, "Root category should be seeded");
//                Assert.That(childFromDb, Is.Not.Null, "Child category should be seeded");

//                Assert.That(childFromDb.ParentCategoryId, Is.EqualTo(1), "ParentCategoryId should be set correctly");

//                // 確認後にロールバック
//                transaction.Rollback();
//            }
//            catch
//            {
//                transaction.Rollback();
//                throw;
//            }
//        }
//    }

//    [Test]
//    public async Task ApplyToModelBuilder_WithNullNavigationProperties_ShouldApplySeedData()
//    {
//        // Arrange - ナビゲーションプロパティにnullを含むデータの準備
//        var book = new Book
//        {
//            Id = 1,
//            Title = "Standalone Book",
//            Author = "Unknown",
//            ISBN = "0000000000",
//            PublicationYear = 2000,
//            CategoryId = 1,  // 外部キーは設定するが、ナビゲーションプロパティはnull
//            PublisherId = 1  // 外部キーは設定するが、ナビゲーションプロパティはnull
//        };

//        _entityScanner.RegisterEntity(book);

//        // カテゴリと出版社も追加
//        var category = new Category { Id = 1, Name = "Unknown", Description = "Unknown category" };
//        var publisher = new Publisher { Id = 1, Name = "Unknown", Address = "Unknown address" };

//        _entityScanner.RegisterEntity(category);
//        _entityScanner.RegisterEntity(publisher);

//        // Act & Assert
//        using (var context = await CreateContextWithModelBuilder())
//        {
//            var books = context.Books.ToList();
//            Assert.That(books, Has.Count.EqualTo(1), "Book should be seeded");
//            Assert.That(books[0].CategoryId, Is.EqualTo(1), "CategoryId should be set correctly");
//            Assert.That(books[0].PublisherId, Is.EqualTo(1), "PublisherId should be set correctly");
//        }
//    }

//    [Test]
//    public async Task ApplyToModelBuilder_WithEmptyEntityCollection_ShouldNotThrowException()
//    {
//        // Arrange - エンティティを追加しない

//        // Act & Assert
//        Assert.DoesNotThrow(async () => {
//            using (var context = await CreateContextWithModelBuilder())
//            {
//                // 何もしない、例外が発生しないことを確認
//            }
//        });
//    }

//    /// <summary>
//    /// ModelBuilderにシードデータを適用したDbContextを作成するヘルパーメソッド
//    /// </summary>
//    private async Task<LibraryDbContext> CreateContextWithModelBuilder()
//    {
//        // データベースを削除
//        using (var context = new LibraryDbContext(_options))
//        {
//            await context.Database.EnsureDeletedAsync();
//        }

//        // テスト用のカスタムDbContextを作成
//        using (var testContext = new TestLibraryDbContext(_options, _entityScanner))
//        {
//            // データベースを作成（この過程でOnModelCreatingが呼ばれ、シードデータが適用される）
//            await testContext.Database.EnsureCreatedAsync();
//            //testContext.GetModelBuilderWithBaseConfiguration();

//        }

//        // 新しい通常のコンテキストを返す（テスト検証用）
//        return new LibraryDbContext(_options);
//    }
//}

//// テスト用の拡張メソッド
//public static class DbContextExtensions
//{
//    public static ModelBuilder GetModelBuilderWithBaseConfiguration(this LibraryDbContext context)
//    {
//        var modelBuilder = new ModelBuilder();

//        // OnModelCreatingと同様の設定を適用
//        modelBuilder.Entity<Member>()
//            .HasOne(m => m.Profile)
//            .WithOne(p => p.Member)
//            .HasForeignKey<MemberProfile>(p => p.MemberId);

//        modelBuilder.Entity<Member>()
//            .HasMany(m => m.BorrowRecords)
//            .WithOne(b => b.Member)
//            .HasForeignKey(b => b.MemberId)
//            .OnDelete(DeleteBehavior.Cascade);

//        modelBuilder.Entity<Book>()
//            .HasMany(b => b.BorrowRecords)
//            .WithOne(br => br.Book)
//            .HasForeignKey(br => br.BookId)
//            .OnDelete(DeleteBehavior.Cascade);

//        modelBuilder.Entity<Category>()
//            .HasMany(c => c.Books)
//            .WithOne(b => b.Category)
//            .HasForeignKey(b => b.CategoryId)
//            .OnDelete(DeleteBehavior.Restrict);

//        modelBuilder.Entity<Publisher>()
//            .HasMany(p => p.PublishedBooks)
//            .WithOne(b => b.Publisher)
//            .HasForeignKey(b => b.PublisherId)
//            .OnDelete(DeleteBehavior.Restrict);

//        modelBuilder.Entity<Library>()
//            .HasMany(l => l.Shelves)
//            .WithOne(s => s.Library)
//            .HasForeignKey(s => s.LibraryId)
//            .OnDelete(DeleteBehavior.Cascade);

//        modelBuilder.Entity<Shelf>()
//            .HasMany(s => s.BookInventories)
//            .WithOne(bi => bi.Shelf)
//            .HasForeignKey(bi => bi.ShelfId)
//            .OnDelete(DeleteBehavior.Cascade);

//        modelBuilder.Entity<BookAuthor>()
//            .HasOne(ba => ba.Book)
//            .WithMany(b => b.Authors)
//            .HasForeignKey(ba => ba.BookId);

//        modelBuilder.Entity<BookAuthor>()
//            .HasOne(ba => ba.Author)
//            .WithMany(a => a.Books)
//            .HasForeignKey(ba => ba.AuthorId);

//        modelBuilder.Entity<MemberBookmark>()
//            .HasOne(mb => mb.Member)
//            .WithMany(m => m.Bookmarks)
//            .HasForeignKey(mb => mb.MemberId);

//        modelBuilder.Entity<MemberBookmark>()
//            .HasOne(mb => mb.Book)
//            .WithMany(b => b.BookmarkedBy)
//            .HasForeignKey(mb => mb.BookId);

//        modelBuilder.Entity<Category>()
//            .HasOne(c => c.ParentCategory)
//            .WithMany(c => c.SubCategories)
//            .HasForeignKey(c => c.ParentCategoryId)
//            .OnDelete(DeleteBehavior.Restrict);

//        modelBuilder.Entity<Book>()
//            .HasIndex(b => b.ISBN)
//            .IsUnique();

//        modelBuilder.Entity<Member>()
//            .HasIndex(m => m.Email)
//            .IsUnique();

//        modelBuilder.Entity<BookInventory>()
//            .HasIndex(bi => new { bi.BookId, bi.CopyNumber })
//            .IsUnique();

//        return modelBuilder;
//    }
//}

// テスト用のLibraryDbContextのサブクラス
public class TestLibraryDbContext : LibraryDbContext
{
    private readonly EntityScanner _entityScanner;

    public TestLibraryDbContext(DbContextOptions<LibraryDbContext> options, EntityScanner entityScanner)
        : base(options)
    {
        _entityScanner = entityScanner ?? throw new ArgumentNullException(nameof(entityScanner));
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        if (modelBuilder == null)
        {
            throw new ArgumentNullException(nameof(modelBuilder));
        }

        // 基底クラスのOnModelCreatingを呼び出して、基本的なモデル構成を適用
        base.OnModelCreating(modelBuilder);

        // エンティティスキャナーを使用してシードデータを適用
        try
        {
            _entityScanner.ApplyToModelBuilder(modelBuilder);
        }
        catch (Exception ex)
        {
            // ログ出力などのエラーハンドリング
            Console.WriteLine($"シードデータ適用中にエラーが発生: {ex.Message}");
            throw; // 再スロー
        }
    }
}
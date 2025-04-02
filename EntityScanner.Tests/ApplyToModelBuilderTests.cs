using EntityScanner.Tests.DbContexts;
using EntityScanner.Tests.Entities;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using System;
using System.Linq;
using System.IO;
using System.Reflection;

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
            .UseSqlite(connectionString)
            .EnableSensitiveDataLogging()
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

    private DbContextOptions<LibraryDbContext> _options = null;
    private string _connectionString;
    private EntityScanner _entityScanner = null;

    [Test]
    public void ApplyToModelBuilder_WithSimpleEntity_ShouldApplySeedData()
    {
        // Arrange
        var category = new Category { Id = 1, Name = "Fiction", Description = "Fiction books" };
        _entityScanner.RegisterEntity(category);

        // Act & Assert
        using (var context = CreateContextWithModelBuilder())
        {
            var categories = context.Categories.ToList();
            Assert.That(categories, Has.Count.EqualTo(1), "Category should be seeded");
            Assert.That(categories[0].Name, Is.EqualTo("Fiction"), "Category name should match");
            Assert.That(categories[0].Description, Is.EqualTo("Fiction books"), "Category description should match");
        }
    }

    [Test]
    public void ApplyToModelBuilder_WithMultipleEntities_ShouldApplySeedData()
    {
        // Arrange
        var category1 = new Category { Id = 1, Name = "Fiction", Description = "Fiction books" };
        var category2 = new Category { Id = 2, Name = "Non-Fiction", Description = "Non-fiction books" };

        _entityScanner.RegisterEntity(category1);
        _entityScanner.RegisterEntity(category2);

        // Act & Assert
        using (var context = CreateContextWithModelBuilder())
        {
            var categories = context.Categories.ToList();
            Assert.That(categories, Has.Count.EqualTo(2), "Both categories should be seeded");
            Assert.That(categories.Any(c => c.Name == "Fiction"), Is.True, "Fiction category should be seeded");
            Assert.That(categories.Any(c => c.Name == "Non-Fiction"), Is.True, "Non-Fiction category should be seeded");
        }
    }

    [Test]
    public void ApplyToModelBuilder_WithRelatedEntities_ShouldApplySeedData()
    {
        // Arrange
        var category = new Category { Id = 1, Name = "Programming", Description = "Programming books" };
        var publisher = new Publisher { Id = 1, Name = "Tech Books", Address = "123 Tech St" };

        var book = new Book
        {
            Id = 1,
            Title = "C# in Depth",
            Author = "Jon Skeet",
            ISBN = "978-1617294532",
            PublicationYear = 2019,
            Category = category,
            Publisher = publisher
        };

        _entityScanner.RegisterEntity(book);

        // Act & Assert
        using (var context = CreateContextWithModelBuilder())
        {
            var books = context.Books.Include(b => b.Category).Include(b => b.Publisher).ToList();
            Assert.That(books, Has.Count.EqualTo(1), "Book should be seeded");

            var loadedBook = books.First();
            Assert.That(loadedBook.Title, Is.EqualTo("C# in Depth"), "Book title should match");
            Assert.That(loadedBook.CategoryId, Is.EqualTo(1), "CategoryId should be set correctly");
            Assert.That(loadedBook.PublisherId, Is.EqualTo(1), "PublisherId should be set correctly");

            var categories = context.Categories.ToList();
            Assert.That(categories, Has.Count.EqualTo(1), "Category should be seeded");

            var publishers = context.Publishers.ToList();
            Assert.That(publishers, Has.Count.EqualTo(1), "Publisher should be seeded");
        }
    }

    [Test]
    public void ApplyToModelBuilder_WithManyToManyRelationship_ShouldApplySeedData()
    {
        // Arrange
        var author = new Author { Id = 1, Name = "John Smith", Biography = "Famous author" };
        var category = new Category { Id = 1, Name = "Fiction", Description = "Fiction books" };
        var publisher = new Publisher { Id = 1, Name = "Big Publisher", Address = "Publisher St" };

        var book = new Book
        {
            Id = 1,
            Title = "Sample Book",
            Author = "John Smith",
            ISBN = "123456789",
            PublicationYear = 2022,
            Category = category,
            Publisher = publisher
        };

        var bookAuthor = new BookAuthor
        {
            Id = 1,
            Book = book,
            Author = author,
            Role = "Main Author"
        };

        book.Authors.Add(bookAuthor);
        author.Books.Add(bookAuthor);

        _entityScanner.RegisterEntity(book);

        // Act & Assert
        using (var context = CreateContextWithModelBuilder())
        {
            var books = context.Books.ToList();
            Assert.That(books, Has.Count.EqualTo(1), "Book should be seeded");

            var authors = context.Authors.ToList();
            Assert.That(authors, Has.Count.EqualTo(1), "Author should be seeded");

            var bookAuthors = context.BookAuthors.ToList();
            Assert.That(bookAuthors, Has.Count.EqualTo(1), "BookAuthor join entity should be seeded");
            Assert.That(bookAuthors[0].BookId, Is.EqualTo(1), "BookId should be set correctly");
            Assert.That(bookAuthors[0].AuthorId, Is.EqualTo(1), "AuthorId should be set correctly");
        }
    }

    [Test]
    public void ApplyToModelBuilder_WithCircularReferences_ShouldApplySeedData()
    {
        // Arrange - 循環参照を持つデータの準備
        var member = new Member
        {
            Id = 1,
            Name = "John Doe",
            Email = "john@example.com",
            RegistrationDate = DateTime.Now.Date
        };

        var profile = new MemberProfile
        {
            Id = 1,
            PhoneNumber = "123-456-7890",
            Address = "123 Main St",
            Member = member,
            MemberId = 1
        };

        member.Profile = profile;

        _entityScanner.RegisterEntity(member);

        // Act & Assert
        using (var context = CreateContextWithModelBuilder())
        {
            var members = context.Members.ToList();
            Assert.That(members, Has.Count.EqualTo(1), "Member should be seeded");

            var profiles = context.MemberProfiles.ToList();
            Assert.That(profiles, Has.Count.EqualTo(1), "Profile should be seeded");
            Assert.That(profiles[0].MemberId, Is.EqualTo(1), "MemberId should be set correctly");
        }
    }

    [Test]
    public void ApplyToModelBuilder_WithHierarchicalData_ShouldApplySeedData()
    {
        Console.WriteLine("ApplyToModelBuilder_WithHierarchicalData_ShouldApplySeedData テストを開始");

        // Arrange - 階層構造を持つデータの準備
        var rootCategory = new Category { Id = 1, Name = "Root", Description = "Root category" };
        var childCategory = new Category
        {
            Id = 2,
            Name = "Child",
            Description = "Child category",
            ParentCategory = rootCategory,
            ParentCategoryId = 1
        };

        rootCategory.SubCategories.Add(childCategory);

        Console.WriteLine("エンティティを登録します");
        Console.WriteLine($"rootCategory: Id={rootCategory.Id}, Name={rootCategory.Name}");
        Console.WriteLine($"childCategory: Id={childCategory.Id}, Name={childCategory.Name}, ParentCategoryId={childCategory.ParentCategoryId}");

        _entityScanner.RegisterEntity(rootCategory);

        // Act & Assert
        Console.WriteLine("コンテキストを作成します");
        using (var context = CreateContextWithModelBuilder())
        {
            Console.WriteLine("作成されたデータベースの内容を検証します");
            var categories = context.Categories.ToList();
            Console.WriteLine($"取得したカテゴリ数: {categories.Count}");

            foreach (var cat in categories)
            {
                Console.WriteLine($"カテゴリ: Id={cat.Id}, Name={cat.Name}, ParentCategoryId={cat.ParentCategoryId}");
            }

            Assert.That(categories, Has.Count.EqualTo(2), "Both categories should be seeded");

            var childFromDb = categories.FirstOrDefault(c => c.Id == 2);
            if (childFromDb != null)
            {
                Console.WriteLine($"子カテゴリを検証: Id={childFromDb.Id}, ParentCategoryId={childFromDb.ParentCategoryId}");
                Assert.That(childFromDb.ParentCategoryId, Is.EqualTo(1), "ParentCategoryId should be set correctly");
            }
            else
            {
                Console.WriteLine("子カテゴリが見つかりません");
            }
        }
    }

    [Test]
    public void ApplyToModelBuilder_WithNullNavigationProperties_ShouldApplySeedData()
    {
        // Arrange - ナビゲーションプロパティにnullを含むデータの準備
        var book = new Book
        {
            Id = 1,
            Title = "Standalone Book",
            Author = "Unknown",
            ISBN = "0000000000",
            PublicationYear = 2000,
            CategoryId = 1,  // 外部キーは設定するが、ナビゲーションプロパティはnull
            PublisherId = 1  // 外部キーは設定するが、ナビゲーションプロパティはnull
        };

        _entityScanner.RegisterEntity(book);

        // カテゴリと出版社も追加
        var category = new Category { Id = 1, Name = "Unknown", Description = "Unknown category" };
        var publisher = new Publisher { Id = 1, Name = "Unknown", Address = "Unknown address" };

        _entityScanner.RegisterEntity(category);
        _entityScanner.RegisterEntity(publisher);

        // Act & Assert
        using (var context = CreateContextWithModelBuilder())
        {
            var books = context.Books.ToList();
            Assert.That(books, Has.Count.EqualTo(1), "Book should be seeded");
            Assert.That(books[0].CategoryId, Is.EqualTo(1), "CategoryId should be set correctly");
            Assert.That(books[0].PublisherId, Is.EqualTo(1), "PublisherId should be set correctly");
        }
    }

    [Test]
    public void ApplyToModelBuilder_WithEmptyEntityCollection_ShouldNotThrowException()
    {
        // Arrange - エンティティを追加しない

        // Act & Assert
        Assert.DoesNotThrow(() => {
            using (var context = CreateContextWithModelBuilder())
            {
                // 何もしない、例外が発生しないことを確認
            }
        });
    }

    /// <summary>
    /// ModelBuilderにシードデータを適用したDbContextを作成するヘルパーメソッド
    /// </summary>
    private LibraryDbContext CreateContextWithModelBuilder()
    {
        Console.WriteLine("CreateContextWithModelBuilder メソッドを開始");

        // まず、テスト用のデータベースを削除する
        using (var context = new LibraryDbContext(_options))
        {
            Console.WriteLine("データベースを削除します");
            context.Database.EnsureDeleted();
        }

        Console.WriteLine("TestLibraryDbContext を作成します");
        // テスト用のカスタムDbContextを作成
        // このコンストラクタでEntityScannerを受け取り、OnModelCreatingで使用する
        var testContext = new TestLibraryDbContext(_options, _entityScanner);

        Console.WriteLine("データベースを作成します（この過程でOnModelCreatingが呼ばれます）");
        // データベースを作成（この過程でOnModelCreatingが呼ばれ、シードデータが適用される）
        testContext.Database.EnsureCreated();

        Console.WriteLine("変更を保存します");
        // データベースに変更を保存
        testContext.SaveChanges();

        Console.WriteLine("新しいコンテキストを返します");
        // 新しい通常のコンテキストを返す（テスト検証用）
        return new LibraryDbContext(_options);
    }
}

// テスト用の拡張メソッド
public static class DbContextExtensions
{
    public static ModelBuilder GetModelBuilderWithBaseConfiguration(this LibraryDbContext context)
    {
        var modelBuilder = new ModelBuilder();

        // OnModelCreatingと同様の設定を適用
        modelBuilder.Entity<Member>()
            .HasOne(m => m.Profile)
            .WithOne(p => p.Member)
            .HasForeignKey<MemberProfile>(p => p.MemberId);

        modelBuilder.Entity<Member>()
            .HasMany(m => m.BorrowRecords)
            .WithOne(b => b.Member)
            .HasForeignKey(b => b.MemberId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Book>()
            .HasMany(b => b.BorrowRecords)
            .WithOne(br => br.Book)
            .HasForeignKey(br => br.BookId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Category>()
            .HasMany(c => c.Books)
            .WithOne(b => b.Category)
            .HasForeignKey(b => b.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Publisher>()
            .HasMany(p => p.PublishedBooks)
            .WithOne(b => b.Publisher)
            .HasForeignKey(b => b.PublisherId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Library>()
            .HasMany(l => l.Shelves)
            .WithOne(s => s.Library)
            .HasForeignKey(s => s.LibraryId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Shelf>()
            .HasMany(s => s.BookInventories)
            .WithOne(bi => bi.Shelf)
            .HasForeignKey(bi => bi.ShelfId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<BookAuthor>()
            .HasOne(ba => ba.Book)
            .WithMany(b => b.Authors)
            .HasForeignKey(ba => ba.BookId);

        modelBuilder.Entity<BookAuthor>()
            .HasOne(ba => ba.Author)
            .WithMany(a => a.Books)
            .HasForeignKey(ba => ba.AuthorId);

        modelBuilder.Entity<MemberBookmark>()
            .HasOne(mb => mb.Member)
            .WithMany(m => m.Bookmarks)
            .HasForeignKey(mb => mb.MemberId);

        modelBuilder.Entity<MemberBookmark>()
            .HasOne(mb => mb.Book)
            .WithMany(b => b.BookmarkedBy)
            .HasForeignKey(mb => mb.BookId);

        modelBuilder.Entity<Category>()
            .HasOne(c => c.ParentCategory)
            .WithMany(c => c.SubCategories)
            .HasForeignKey(c => c.ParentCategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Book>()
            .HasIndex(b => b.ISBN)
            .IsUnique();

        modelBuilder.Entity<Member>()
            .HasIndex(m => m.Email)
            .IsUnique();

        modelBuilder.Entity<BookInventory>()
            .HasIndex(bi => new { bi.BookId, bi.CopyNumber })
            .IsUnique();

        return modelBuilder;
    }
}

// テスト用のLibraryDbContextのサブクラス
public class TestLibraryDbContext : LibraryDbContext
{
    private readonly EntityScanner _entityScanner;

    public TestLibraryDbContext(DbContextOptions<LibraryDbContext> options, EntityScanner entityScanner)
        : base(options)
    {
        _entityScanner = entityScanner;
        Console.WriteLine("TestLibraryDbContext コンストラクタが呼び出されました");
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        Console.WriteLine("TestLibraryDbContext.OnModelCreating が呼び出されました");

        // 基底クラスのOnModelCreatingを呼び出して、基本的なモデル構成を適用
        base.OnModelCreating(modelBuilder);
        Console.WriteLine("base.OnModelCreating が完了しました");

        // エンティティスキャナーを使用してシードデータを適用
        Console.WriteLine("EntityScannerを使用してシードデータを適用します");
        _entityScanner.ApplyToModelBuilder(modelBuilder);
        Console.WriteLine("シードデータの適用が完了しました");
    }
}
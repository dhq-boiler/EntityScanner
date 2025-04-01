using EntityScanner.Tests.DbContexts;
using EntityScanner.Tests.Entities;
using Microsoft.EntityFrameworkCore;

namespace EntityScanner.Tests;

[TestFixture]
public class ApplyToContextTests
{
    [SetUp]
    public void Setup()
    {
        // SQLiteを使用するためのユニークなDB接続文字列を用意
        var connectionString = $"Data Source=ApplyToContextTests_{Guid.NewGuid()}.db";
        _options = new DbContextOptionsBuilder<LibraryDbContext>()
            .UseSqlite(connectionString)
            .EnableSensitiveDataLogging()
            .Options;

        // 接続文字列を保存
        _connectionString = connectionString;

        // EntityScannerを初期化
        _entityScanner = new EntityScanner();

        // 各テスト前にDBを作成
        using (var context = new LibraryDbContext(_options))
        {
            context.Database.EnsureCreated();
        }
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
    public void ApplyToContext_MultipleEntitiesOfSameType_ShouldAddAllToDbContext()
    {
        // Arrange - 同じタイプの複数エンティティを準備
        var category1 = new Category { Id = 1, Name = "Fiction", Description = "Fiction books" };
        var category2 = new Category { Id = 2, Name = "Non-Fiction", Description = "Non-fiction books" };
        var category3 = new Category { Id = 3, Name = "Programming", Description = "Programming books" };

        _entityScanner.RegisterEntity(category1);
        _entityScanner.RegisterEntity(category2);
        _entityScanner.RegisterEntity(category3);

        // Act
        using (var context = new LibraryDbContext(_options))
        {
            _entityScanner.ApplyToContext(context);
            context.SaveChanges();
        }

        // Assert
        using (var context = new LibraryDbContext(_options))
        {
            var categories = context.Categories.ToList();
            Assert.That(categories.Count, Is.EqualTo(3), "All three categories should be added");
            Assert.That(categories.Select(c => c.Name).Contains("Fiction"), Is.True, "Fiction category should exist");
            Assert.That(categories.Select(c => c.Name).Contains("Non-Fiction"), Is.True, "Non-Fiction category should exist");
            Assert.That(categories.Select(c => c.Name).Contains("Programming"), Is.True, "Programming category should exist");
        }
    }

    [Test]
    public void ApplyToContext_MultipleEntityTypes_ShouldAddAllToDbContext()
    {
        // Arrange - 複数の異なるタイプのエンティティを準備
        var category = new Category { Id = 1, Name = "Science Fiction", Description = "Sci-Fi books" };
        var publisher = new Publisher { Id = 1, Name = "Tech Publications", Address = "123 Tech St" };
        var author = new Author { Id = 1, Name = "Jane Doe", Biography = "Famous author" };

        _entityScanner.RegisterEntity(category);
        _entityScanner.RegisterEntity(publisher);
        _entityScanner.RegisterEntity(author);

        // Act
        using (var context = new LibraryDbContext(_options))
        {
            _entityScanner.ApplyToContext(context);
            context.SaveChanges();
        }

        // Assert
        using (var context = new LibraryDbContext(_options))
        {
            Assert.That(context.Categories.Count(), Is.EqualTo(1), "Category should be added");
            Assert.That(context.Publishers.Count(), Is.EqualTo(1), "Publisher should be added");
            Assert.That(context.Authors.Count(), Is.EqualTo(1), "Author should be added");
        }
    }

    [Test]
    public void ApplyToContext_ComplexGraph_ShouldMaintainRelationships()
    {
        // Arrange - 複雑なオブジェクトグラフを準備
        var category = new Category { Id = 1, Name = "Mystery", Description = "Mystery novels" };
        var publisher = new Publisher { Id = 1, Name = "Mystery House", Address = "456 Mystery Rd" };

        var author1 = new Author { Id = 1, Name = "Agatha Christie", Biography = "Famous mystery writer" };
        var author2 = new Author { Id = 2, Name = "Arthur Conan Doyle", Biography = "Creator of Sherlock Holmes" };

        var book1 = new Book
        {
            Id = 1,
            Title = "Murder on the Orient Express",
            Author = "Agatha Christie",
            ISBN = "978-0-00-712674-5",
            PublicationYear = 1934,
            Category = category,
            Publisher = publisher
        };

        var book2 = new Book
        {
            Id = 2,
            Title = "The Hound of the Baskervilles",
            Author = "Arthur Conan Doyle",
            ISBN = "978-0-14-043786-7",
            PublicationYear = 1902,
            Category = category,
            Publisher = publisher
        };

        var bookAuthor1 = new BookAuthor
        {
            Id = 1,
            Book = book1,
            Author = author1,
            Role = "Author"
        };

        var bookAuthor2 = new BookAuthor
        {
            Id = 2,
            Book = book2,
            Author = author2,
            Role = "Author"
        };

        // 関連を設定
        book1.Authors.Add(bookAuthor1);
        author1.Books.Add(bookAuthor1);

        book2.Authors.Add(bookAuthor2);
        author2.Books.Add(bookAuthor2);

        category.Books.Add(book1);
        category.Books.Add(book2);

        publisher.PublishedBooks.Add(book1);
        publisher.PublishedBooks.Add(book2);

        // 登録（book1だけ登録して他は自動的に処理されるか確認）
        _entityScanner.RegisterEntity(book1);
        _entityScanner.RegisterEntity(book2);

        // Act
        using (var context = new LibraryDbContext(_options))
        {
            _entityScanner.ApplyToContext(context);
            context.SaveChanges();
        }

        // Assert
        using (var context = new LibraryDbContext(_options))
        {
            // 基本的なエンティティ数の確認
            Assert.That(context.Books.Count(), Is.EqualTo(2), "Both books should be added");
            Assert.That(context.Categories.Count(), Is.EqualTo(1), "Category should be added");
            Assert.That(context.Publishers.Count(), Is.EqualTo(1), "Publisher should be added");
            Assert.That(context.Authors.Count(), Is.EqualTo(2), "Both authors should be added");
            Assert.That(context.BookAuthors.Count(), Is.EqualTo(2), "Both book-author relationships should be added");

            // 関連の確認（Include使用）
            var loadedBooks = context.Books
                .Include(b => b.Category)
                .Include(b => b.Publisher)
                .Include(b => b.Authors)
                .ThenInclude(ba => ba.Author)
                .ToList();

            Assert.That(loadedBooks.Count, Is.EqualTo(2), "Both books should be loaded");

            var book1Loaded = loadedBooks.FirstOrDefault(b => b.Title == "Murder on the Orient Express");
            var book2Loaded = loadedBooks.FirstOrDefault(b => b.Title == "The Hound of the Baskervilles");

            Assert.That(book1Loaded, Is.Not.Null, "First book should be loaded");
            Assert.That(book2Loaded, Is.Not.Null, "Second book should be loaded");

            // 書籍1の関連確認
            Assert.That(book1Loaded.Category.Name, Is.EqualTo("Mystery"), "Book1 should have correct category");
            Assert.That(book1Loaded.Publisher.Name, Is.EqualTo("Mystery House"), "Book1 should have correct publisher");
            Assert.That(book1Loaded.Authors.Count, Is.EqualTo(1), "Book1 should have one author");
            Assert.That(book1Loaded.Authors[0].Author.Name, Is.EqualTo("Agatha Christie"), "Book1 should have correct author");

            // 書籍2の関連確認
            Assert.That(book2Loaded.Category.Name, Is.EqualTo("Mystery"), "Book2 should have correct category");
            Assert.That(book2Loaded.Publisher.Name, Is.EqualTo("Mystery House"), "Book2 should have correct publisher");
            Assert.That(book2Loaded.Authors.Count, Is.EqualTo(1), "Book2 should have one author");
            Assert.That(book2Loaded.Authors[0].Author.Name, Is.EqualTo("Arthur Conan Doyle"), "Book2 should have correct author");
        }
    }

    [Test]
    public void ApplyToContext_WithNewEntities_ShouldAddThemToDatabase()
    {
        // Arrange - カテゴリと出版社も一緒に登録
        var category = new Category { Id = 1, Name = "Fiction", Description = "Fiction books" };
        var publisher = new Publisher { Id = 1, Name = "Test Publisher", Address = "Publisher Address" };
        var book = new Book
        {
            Id = 1,
            Title = "Test Book",
            Author = "Test Author",
            ISBN = "1234567890",
            PublicationYear = 2022,
            Category = category,
            CategoryId = 1,
            Publisher = publisher,
            PublisherId = 1
        };

        // カテゴリ、出版社、本の順に登録
        _entityScanner.RegisterEntity(category);
        _entityScanner.RegisterEntity(publisher);
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
            Assert.That(context.Books.Count(), Is.EqualTo(1));
            Assert.That(context.Categories.Count(), Is.EqualTo(1));
            Assert.That(context.Publishers.Count(), Is.EqualTo(1));

            var savedBook = context.Books.Include(b => b.Category).Include(b => b.Publisher).First();
            Assert.That(savedBook.Title, Is.EqualTo("Test Book"));
            Assert.That(savedBook.Category.Name, Is.EqualTo("Fiction"));
            Assert.That(savedBook.Publisher.Name, Is.EqualTo("Test Publisher"));
        }
    }

    [Test]
    public void ApplyToContext_WithExistingEntities_ShouldNotDuplicateThem()
    {
        // Arrange - First add entities to database
        var category = new Category { Id = 1, Name = "Fiction", Description = "Fiction books" };
        var publisher = new Publisher { Id = 1, Name = "Test Publisher", Address = "Test Address" };
        var book = new Book
        {
            Id = 1,
            Title = "Test Book",
            Author = "Test Author",
            ISBN = "1234567890",
            PublicationYear = 2022,
            Category = category,
            CategoryId = 1,
            Publisher = publisher,
            PublisherId = 1
        };

        using (var context = new LibraryDbContext(_options))
        {
            context.Categories.Add(category);
            context.Publishers.Add(publisher);
            context.Books.Add(book);
            context.SaveChanges();
        }

        // Now try to apply the same entities again
        var sameCategory = new Category { Id = 1, Name = "Fiction", Description = "Fiction books" };
        var samePublisher = new Publisher { Id = 1, Name = "Test Publisher", Address = "Test Address" };
        var sameBook = new Book
        {
            Id = 1,
            Title = "Test Book",
            Author = "Test Author",
            ISBN = "1234567890",
            PublicationYear = 2022,
            Category = sameCategory,
            CategoryId = 1,
            Publisher = samePublisher,
            PublisherId = 1
        };

        _entityScanner.RegisterEntity(sameBook);

        // Act & Assert
        using (var context = new LibraryDbContext(_options))
        {
            try
            {
                _entityScanner.ApplyToContext(context);
                context.SaveChanges();
            }
            catch (InvalidOperationException ex)
            {
                // ここで例外の内容を検証
                Assert.That(ex.Message, Does.Contain("An entity with the same primary key"));
            }

            // Verify that no duplicate entities were created
            Assert.That(context.Books.Count(), Is.EqualTo(1), "No duplicate books should be added");
            Assert.That(context.Categories.Count(), Is.EqualTo(1), "No duplicate categories should be added");
            Assert.That(context.Publishers.Count(), Is.EqualTo(1), "No duplicate publishers should be added");
        }
    }

    [Test]
    public void ApplyToContext_WithUpdatedEntities_ShouldUpdateExistingEntities()
    {
        // Arrange - First add entities to database
        var category = new Category { Id = 1, Name = "Fiction", Description = "Fiction books" };
        var publisher = new Publisher { Id = 1, Name = "Test Publisher", Address = "Test Address" };
        var book = new Book
        {
            Id = 1,
            Title = "Test Book",
            Author = "Test Author",
            ISBN = "1234567890",
            PublicationYear = 2022,
            Category = category,
            CategoryId = 1,
            Publisher = publisher,
            PublisherId = 1
        };

        using (var context = new LibraryDbContext(_options))
        {
            // Explicitly add all required related entities
            context.Categories.Add(category);
            context.Publishers.Add(publisher);
            context.Books.Add(book);
            context.SaveChanges();
        }

        // Now update entities
        var updatedCategory = new Category { Id = 1, Name = "Updated Fiction", Description = "Updated fiction books" };
        var updatedPublisher = new Publisher { Id = 1, Name = "Updated Publisher", Address = "Test Address" }; // Keep original address
        var updatedBook = new Book
        {
            Id = 1,
            Title = "Updated Test Book",
            Author = "Updated Author",
            ISBN = "0987654321", // Changed ISBN
            PublicationYear = 2023, // Changed year
            Category = updatedCategory,
            CategoryId = 1,
            Publisher = updatedPublisher,
            PublisherId = 1
        };

        _entityScanner.RegisterEntity(updatedBook);

        // Act
        using (var context = new LibraryDbContext(_options))
        {
            _entityScanner.ApplyToContext(context);
            context.SaveChanges();
        }

        // Assert
        using (var context = new LibraryDbContext(_options))
        {
            var updatedSavedBook = context.Books
                .Include(b => b.Category)
                .Include(b => b.Publisher)
                .First();

            Assert.That(updatedSavedBook.Title, Is.EqualTo("Updated Test Book"));
            Assert.That(updatedSavedBook.Author, Is.EqualTo("Updated Author"));
            Assert.That(updatedSavedBook.ISBN, Is.EqualTo("0987654321"));
            Assert.That(updatedSavedBook.PublicationYear, Is.EqualTo(2023));

            // Verify updated category
            Assert.That(updatedSavedBook.Category.Name, Is.EqualTo("Updated Fiction"));
            Assert.That(updatedSavedBook.Category.Description, Is.EqualTo("Updated fiction books"));

            // Verify publisher updates
            Assert.That(updatedSavedBook.Publisher.Name, Is.EqualTo("Updated Publisher"));
            Assert.That(updatedSavedBook.Publisher.Address, Is.EqualTo("Test Address"));
        }
    }

    [Test]
    public void ApplyToContext_WithCircularReferences_ShouldHandleCorrectly()
    {
        // Arrange - 循環参照を含むオブジェクトグラフを準備
        var member = new Member
        {
            Id = 1,
            Name = "John Doe",
            Email = "john@example.com",
            RegistrationDate = DateTime.Now
        };

        var profile = new MemberProfile
        {
            Id = 1,
            PhoneNumber = "123-456-7890",
            Address = "123 Main St",
            Member = member
        };

        // 循環参照を設定
        member.Profile = profile;

        _entityScanner.RegisterEntity(member);

        // Act
        using (var context = new LibraryDbContext(_options))
        {
            _entityScanner.ApplyToContext(context);
            context.SaveChanges();
        }

        // Assert
        using (var context = new LibraryDbContext(_options))
        {
            Assert.That(context.Members.Count(), Is.EqualTo(1), "Member should be added");
            Assert.That(context.MemberProfiles.Count(), Is.EqualTo(1), "Profile should be added");

            var loadedMember = context.Members
                .Include(m => m.Profile)
                .FirstOrDefault();

            Assert.That(loadedMember, Is.Not.Null, "Member should be loaded");
            Assert.That(loadedMember.Profile, Is.Not.Null, "Member's profile should be loaded");
            Assert.That(loadedMember.Profile.MemberId, Is.EqualTo(1), "Foreign key should be set correctly");
        }
    }

    [Test]
    public void ApplyToContext_WithHierarchicalData_ShouldHandleCorrectly()
    {
        // Arrange - 階層データを準備
        var rootCategory = new Category
        {
            Id = 1,
            Name = "Books",
            Description = "All books"
        };

        var subCategory1 = new Category
        {
            Id = 2,
            Name = "Fiction",
            Description = "Fiction books",
            ParentCategory = rootCategory
        };

        var subCategory2 = new Category
        {
            Id = 3,
            Name = "Non-Fiction",
            Description = "Non-fiction books",
            ParentCategory = rootCategory
        };

        var subSubCategory = new Category
        {
            Id = 4,
            Name = "Science Fiction",
            Description = "Sci-fi books",
            ParentCategory = subCategory1
        };

        // 親子関係を設定
        rootCategory.SubCategories.Add(subCategory1);
        rootCategory.SubCategories.Add(subCategory2);
        subCategory1.SubCategories.Add(subSubCategory);

        _entityScanner.RegisterEntity(rootCategory);

        // Act
        using (var context = new LibraryDbContext(_options))
        {
            _entityScanner.ApplyToContext(context);
            context.SaveChanges();
        }

        // Assert
        using (var context = new LibraryDbContext(_options))
        {
            var categories = context.Categories.ToList();
            Assert.That(categories.Count, Is.EqualTo(4), "All categories should be added");

            // 階層構造をIncludeで取得して確認
            var rootFromDb = context.Categories
                .Include(c => c.SubCategories)
                .ThenInclude(c => c.SubCategories)
                .FirstOrDefault(c => c.ParentCategoryId == null);

            Assert.That(rootFromDb, Is.Not.Null, "Root category should be retrievable");
            Assert.That(rootFromDb.Name, Is.EqualTo("Books"), "Root category name should be correct");
            Assert.That(rootFromDb.SubCategories.Count, Is.EqualTo(2), "Root should have 2 subcategories");

            var fiction = rootFromDb.SubCategories.FirstOrDefault(c => c.Name == "Fiction");
            Assert.That(fiction, Is.Not.Null, "Fiction subcategory should exist");
            Assert.That(fiction.SubCategories.Count, Is.EqualTo(1), "Fiction should have 1 subcategory");
            Assert.That(fiction.SubCategories[0].Name, Is.EqualTo("Science Fiction"), "SciFi subcategory name should be correct");
        }
    }

    [Test]
    public void ApplyToContext_MultipleRelatedEntities_ShouldTrackCorrectly()
    {
        // Arrange - 複数の関連エンティティを準備
        var library = new Library { Id = 1, Name = "Central Library", Address = "123 Library St" };

        var shelf1 = new Shelf { Id = 1, ShelfCode = "A1", Location = "Floor 1", Library = library };
        var shelf2 = new Shelf { Id = 2, ShelfCode = "A2", Location = "Floor 1", Library = library };
        var shelf3 = new Shelf { Id = 3, ShelfCode = "B1", Location = "Floor 2", Library = library };

        library.Shelves.Add(shelf1);
        library.Shelves.Add(shelf2);
        library.Shelves.Add(shelf3);

        var category = new Category { Id = 1, Name = "Reference", Description = "Reference books" };
        var publisher = new Publisher { Id = 1, Name = "Academic Press", Address = "456 Academic Blvd" };

        var book1 = new Book
        {
            Id = 1,
            Title = "Encyclopedia Vol 1",
            ISBN = "123-456-789",
            PublicationYear = 2020,
            Category = category,
            Publisher = publisher,
            Author = "Various"
        };

        var book2 = new Book
        {
            Id = 2,
            Title = "Encyclopedia Vol 2",
            ISBN = "123-456-790",
            PublicationYear = 2020,
            Category = category,
            Publisher = publisher,
            Author = "Various"
        };

        // 本の在庫を複数棚に配置
        var inventory1 = new BookInventory
        {
            Id = 1,
            Book = book1,
            Shelf = shelf1,
            CopyNumber = "REF001-1",
            Status = BookInventory.StatusEnum.Available
        };

        var inventory2 = new BookInventory
        {
            Id = 2,
            Book = book1,
            Shelf = shelf2,
            CopyNumber = "REF001-2",
            Status = BookInventory.StatusEnum.Available
        };

        var inventory3 = new BookInventory
        {
            Id = 3,
            Book = book2,
            Shelf = shelf3,
            CopyNumber = "REF002-1",
            Status = BookInventory.StatusEnum.Available
        };

        // 関連を設定
        shelf1.BookInventories.Add(inventory1);
        shelf2.BookInventories.Add(inventory2);
        shelf3.BookInventories.Add(inventory3);

        // 登録（libraryのみ登録して他は自動処理されるか確認）
        _entityScanner.RegisterEntity(library);

        // Act
        using (var context = new LibraryDbContext(_options))
        {
            _entityScanner.ApplyToContext(context);
            context.SaveChanges();
        }

        // Assert
        using (var context = new LibraryDbContext(_options))
        {
            // 各エンティティタイプの数を確認
            Assert.That(context.Libraries.Count(), Is.EqualTo(1), "Library should be added");
            Assert.That(context.Shelves.Count(), Is.EqualTo(3), "All shelves should be added");
            Assert.That(context.Books.Count(), Is.EqualTo(2), "Both books should be added");
            Assert.That(context.BookInventories.Count(), Is.EqualTo(3), "All inventories should be added");

            // 図書館と棚の関係を確認
            var loadedLibrary = context.Libraries
                .Include(l => l.Shelves)
                .FirstOrDefault();

            Assert.That(loadedLibrary, Is.Not.Null, "Library should be loaded");
            Assert.That(loadedLibrary.Shelves.Count, Is.EqualTo(3), "Library should have 3 shelves");

            // 棚と本の在庫の関係を確認
            var loadedShelves = context.Shelves
                .Include(s => s.BookInventories)
                .ThenInclude(bi => bi.Book)
                .ToList();

            var shelf1Loaded = loadedShelves.FirstOrDefault(s => s.ShelfCode == "A1");
            var shelf2Loaded = loadedShelves.FirstOrDefault(s => s.ShelfCode == "A2");
            var shelf3Loaded = loadedShelves.FirstOrDefault(s => s.ShelfCode == "B1");

            Assert.That(shelf1Loaded.BookInventories.Count, Is.EqualTo(1), "Shelf A1 should have 1 inventory");
            Assert.That(shelf2Loaded.BookInventories.Count, Is.EqualTo(1), "Shelf A2 should have 1 inventory");
            Assert.That(shelf3Loaded.BookInventories.Count, Is.EqualTo(1), "Shelf B1 should have 1 inventory");

            Assert.That(shelf1Loaded.BookInventories[0].Book.Title, Is.EqualTo("Encyclopedia Vol 1"), "Shelf A1 should have Encyclopedia Vol 1");
            Assert.That(shelf2Loaded.BookInventories[0].Book.Title, Is.EqualTo("Encyclopedia Vol 1"), "Shelf A2 should have Encyclopedia Vol 1");
            Assert.That(shelf3Loaded.BookInventories[0].Book.Title, Is.EqualTo("Encyclopedia Vol 2"), "Shelf B1 should have Encyclopedia Vol 2");
        }
    }
}
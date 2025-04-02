using EntityScanner.Tests.DbContexts;
using EntityScanner.Tests.Entities;
using Microsoft.EntityFrameworkCore;

namespace EntityScanner.Tests;

[TestFixture]
public class EntityScannerErrorHandlingTests
{
    [SetUp]
    public void Setup()
    {
        // SQLiteを使用するためのユニークなDB接続文字列を用意
        var connectionString = $"Data Source=ErrorHandlingTests_{Guid.NewGuid()}.db";
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

    private DbContextOptions<LibraryDbContext> _options;
    private string _connectionString;
    private EntityScanner _entityScanner;

    [Test]
    public void ApplyToContext_WithNullContext_ShouldThrowArgumentNullException()
    {
        // Arrange
        var category = new Category { Id = 1, Name = "Test" };
        _entityScanner.RegisterEntity(category);

        // Act & Assert
        var ex = Assert.Throws<ArgumentNullException>(() => _entityScanner.ApplyToContext(null));
        Assert.That(ex.ParamName, Is.EqualTo("context"), "Parameter name should be 'context'");
    }


    [Test]
    public void ApplyToContext_WithDuplicateIds_ShouldThrowInvalidOperationException()
    {
        // Arrange - 同じIDを持つ2つのエンティティを準備
        var category1 = new Category { Id = 1, Name = "Fiction" };
        var category2 = new Category { Id = 1, Name = "Duplicate ID" }; // 同じID

        _entityScanner.RegisterEntity(category1);
        _entityScanner.RegisterEntity(category2);

        // Act & Assert - ApplyToContext 呼び出し時に例外が発生するはず
        using (var context = new LibraryDbContext(_options))
        {
            var ex = Assert.Throws<InvalidOperationException>(() => _entityScanner.ApplyToContext(context));

            // エラーメッセージをチェック
            Assert.That(ex.Message, Does.Contain("is already being tracked"));
        }
    }

    [Test]
    public void ApplyToContext_WithDifferentEntitiesSameId_ShouldThrowException()
    {
        // Arrange - まず1つ目のエンティティを保存する
        var category1 = new Category { Id = 1, Name = "Fiction", Description = "Fiction books" };

        using (var context = new LibraryDbContext(_options))
        {
            context.Categories.Add(category1);
            context.SaveChanges();
        }

        // EntityScannerをクリア
        _entityScanner.Clear();

        // 同じIDで別の名前のエンティティを作成（Descriptionも設定）
        var category2 = new Category
        {
            Id = 1,
            Name = "Different Name",
            Description = "Different description"
        };
        _entityScanner.RegisterEntity(category2);

        // Act & Assert - 別のコンテキストで追加して例外が発生することを確認
        using (var context = new LibraryDbContext(_options))
        {
            Assert.Throws<InvalidOperationException>(() =>
            {
                _entityScanner.ApplyToContext(context);
                context.SaveChanges();
            }, "Should throw an exception when adding an entity with same ID but different properties");
        }
    }

    [Test]
    public void ApplyToContext_WithMissingRequiredFields_ShouldThrowValidationException()
    {
        // Arrange - 必須フィールドが欠けているエンティティを準備
        var category = new Category
        {
            Id = 1,
            Name = null, // Nameは[Required]属性が付いている
            Description = "Test Description" // Description も NOT NULL 制約あり
        };

        _entityScanner.RegisterEntity(category);

        // Act & Assert
        using (var context = new LibraryDbContext(_options))
        {
            _entityScanner.ApplyToContext(context);

            // SaveChanges時に検証例外が発生する
            var ex = Assert.Throws<DbUpdateException>(() => context.SaveChanges());
            Assert.That(ex.InnerException.Message,
                Does.Contain("NOT NULL constraint failed") | Does.Contain("cannot be null"));
        }
    }

    [Test]
    public void ApplyToContext_WithMissingDescriptionField_ShouldThrowDbUpdateException()
    {
        // Arrange - Description フィールドが欠けているエンティティを準備
        var category = new Category
        {
            Id = 1,
            Name = "Test Category", // Required フィールドは設定
            Description = null // Description は NOT NULL 制約あり
        };

        _entityScanner.RegisterEntity(category);

        // Act & Assert
        using (var context = new LibraryDbContext(_options))
        {
            _entityScanner.ApplyToContext(context);

            // SaveChanges時に検証例外が発生する
            var ex = Assert.Throws<DbUpdateException>(() => context.SaveChanges());
            Assert.That(ex.InnerException.Message,
                Does.Contain("NOT NULL constraint failed") | Does.Contain("cannot be null"));
        }
    }

    [Test]
    public void ApplyToContext_WithInvalidForeignKey_ShouldHandleConstraintViolation()
    {
        // Arrange - 存在しない外部キー参照を持つエンティティを準備
        var book = new Book
        {
            Id = 1,
            Title = "Test Book",
            Author = "Test Author",
            ISBN = "1234567890",
            PublicationYear = 2023,
            CategoryId = 999, // 存在しないカテゴリID
            PublisherId = 999 // 存在しない出版社ID
        };

        _entityScanner.RegisterEntity(book);

        // Act & Assert
        using (var context = new LibraryDbContext(_options))
        {
            _entityScanner.ApplyToContext(context);

            // SaveChanges時に外部キー制約違反の例外が発生する
            var ex = Assert.Throws<DbUpdateException>(() => context.SaveChanges());
            Assert.That(ex.InnerException.Message,
                Does.Contain("FOREIGN KEY constraint failed") | Does.Contain("foreign key"));
        }
    }

    [Test]
    public void ApplyToContext_WithMaxLengthViolation_ShouldHandleConstraintViolation()
    {
        // Skip if using SQLite which doesn't enforce character limits by default
        var isSqlite = _options.Extensions.Any(e => e.GetType().Namespace.Contains("Sqlite"));
        if (isSqlite)
        {
            Assert.Ignore("SQLite doesn't enforce MaxLength constraints by default.");
        }

        // Arrange - 最大長を超える値を持つエンティティを準備
        var category = new Category
        {
            Id = 1,
            Name = new string('A', 200), // Name属性には[MaxLength(100)]が付いている
            Description = "Test description"
        };

        _entityScanner.RegisterEntity(category);

        // Act & Assert
        using (var context = new LibraryDbContext(_options))
        {
            _entityScanner.ApplyToContext(context);

            // SaveChanges時に長さ制約違反の例外が発生する
            var ex = Assert.Throws<DbUpdateException>(() => context.SaveChanges());
            // いくつかのデータベースプロバイダーで見られるエラーメッセージをチェック
            Assert.That(ex.InnerException.Message, Does.Contain("String or binary data would be truncated") |
                                                   Does.Contain("data too long") |
                                                   Does.Contain("String data, right truncation"));
        }
    }

    [Test]
    public void ApplyToContext_WithUniqueConstraintViolation_ShouldHandleConstraintViolation()
    {
        // Arrange - ユニーク制約に違反するエンティティを準備

        // 最初に1つ目のエンティティを登録して保存
        var book1 = new Book
        {
            Id = 1,
            Title = "Test Book 1",
            Author = "Test Author",
            ISBN = "1234567890", // ISBNはユニーク制約あり
            PublicationYear = 2023,
            CategoryId = 1,
            PublisherId = 1
        };

        // カテゴリと出版社も登録（外部キー制約を満たすため）
        var category = new Category { Id = 1, Name = "Test Category", Description = "Test category description" };
        var publisher = new Publisher { Id = 1, Name = "Test Publisher", Address = "Test publisher address" };

        _entityScanner.RegisterEntity(category);
        _entityScanner.RegisterEntity(publisher);
        _entityScanner.RegisterEntity(book1);

        using (var context = new LibraryDbContext(_options))
        {
            _entityScanner.ApplyToContext(context);
            context.SaveChanges();
        }

        // EntityScannerをクリア
        _entityScanner.Clear();

        // 同じISBNを持つ2つ目の本を登録
        var book2 = new Book
        {
            Id = 2,
            Title = "Test Book 2",
            Author = "Another Author",
            ISBN = "1234567890", // 同じISBN（ユニーク制約違反）
            PublicationYear = 2023,
            CategoryId = 1,
            PublisherId = 1
        };

        _entityScanner.RegisterEntity(book2);

        // Act & Assert
        using (var context = new LibraryDbContext(_options))
        {
            _entityScanner.ApplyToContext(context);

            // SaveChanges時にユニーク制約違反の例外が発生する
            var ex = Assert.Throws<DbUpdateException>(() => context.SaveChanges());
            Assert.That(ex.InnerException.Message, Does.Contain("UNIQUE constraint failed") |
                                                   Does.Contain("duplicate key value") |
                                                   Does.Contain("violation of UNIQUE KEY constraint"));
        }
    }

    [Test]
    public void ApplyToContext_WithEmptyEntityCollection_ShouldNotThrowException()
    {
        // Arrange - エンティティを登録しない

        // Act & Assert - 例外が発生しないことを確認
        using (var context = new LibraryDbContext(_options))
        {
            Assert.DoesNotThrow(() => _entityScanner.ApplyToContext(context));
            Assert.DoesNotThrow(() => context.SaveChanges());
        }
    }

    // カスタムクラス（DbContextにDbSetプロパティがない）
    public class NonDbSetEntity
    {
        public int Id { get; set; }
        public string Name { get; set; }
    }

    [Test]
    public void ApplyToContext_WithNonExistentDbSet_ShouldIgnoreEntity()
    {
        // Arrange
        var entity = new NonDbSetEntity { Id = 1, Name = "Test" };

        // EntityScannerのメソッドを使用してエンティティを登録
        // 注：通常のRegisterEntityはジェネリック型制約があるため、リフレクションを使用
        var method = typeof(EntityScanner).GetMethod("RegisterEntity");
        var genericMethod = method.MakeGenericMethod(typeof(NonDbSetEntity));
        genericMethod.Invoke(_entityScanner, new object[] { entity });

        // Act & Assert - DbSetがなくても例外が発生しないことを確認
        using (var context = new LibraryDbContext(_options))
        {
            Assert.DoesNotThrow(() => _entityScanner.ApplyToContext(context));
            Assert.DoesNotThrow(() => context.SaveChanges());
        }
    }

    [Test]
    public void ApplyToContext_WithTransactionRollback_ShouldNotPersistChanges()
    {
        // Arrange - すべての必須フィールドを設定しているか確認
        var category = new Category
        {
            Id = 1,
            Name = "Fiction",
            Description = "Fiction books" // NOT NULL 制約のあるフィールド
        };
        var publisher = new Publisher
        {
            Id = 1,
            Name = "Publisher",
            Address = "Publisher Address" // アドレスも設定
        };
        var book = new Book
        {
            Id = 1,
            Title = "Test Book",
            Author = "Test Author",
            ISBN = "1234567890",
            PublicationYear = 2023,
            Category = category,
            Publisher = publisher
        };

        _entityScanner.RegisterEntity(book);

        // Act - トランザクション内でApplyToContextを実行し、ロールバックする
        using (var context = new LibraryDbContext(_options))
        {
            using (var transaction = context.Database.BeginTransaction())
            {
                _entityScanner.ApplyToContext(context);
                context.SaveChanges();

                // 変更がDBに反映されていることを確認
                Assert.That(context.Books.Count(), Is.EqualTo(1));
                Assert.That(context.Categories.Count(), Is.EqualTo(1));
                Assert.That(context.Publishers.Count(), Is.EqualTo(1));

                // ロールバック
                transaction.Rollback();
            }
        }

        // Assert - トランザクションをロールバックしたため、DB内のエンティティは存在しないはず
        using (var context = new LibraryDbContext(_options))
        {
            Assert.That(context.Books.Count(), Is.EqualTo(0));
            Assert.That(context.Categories.Count(), Is.EqualTo(0));
            Assert.That(context.Publishers.Count(), Is.EqualTo(0));
        }
    }
}
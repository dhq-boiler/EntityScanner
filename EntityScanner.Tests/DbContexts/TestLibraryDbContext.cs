using EntityScanner.Tests.Entities;
using Microsoft.EntityFrameworkCore;

namespace EntityScanner.Tests.DbContexts
{
    // ApplyToModelBuilderテスト用のヘルパークラス
    public class TestLibraryDbContext : LibraryDbContext
    {
        private readonly EntityScanner _entityScanner;

        public TestLibraryDbContext(DbContextOptions<LibraryDbContext> options, EntityScanner entityScanner)
            : base(options)
        {
            _entityScanner = entityScanner;
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            try
            {
                // EntityScannerのシードデータをModelBuilderに適用
                _entityScanner.ApplyToModelBuilder(modelBuilder);

                // デバッグのために、エンティティの設定を確認
                var bookEntity = modelBuilder.Entity<Book>();
                var categoryEntity = modelBuilder.Entity<Category>();
            }
            catch (Exception ex)
            {
                // 例外の詳細を出力
                Console.WriteLine($"ApplyToModelBuilder例外: {ex.Message}");
                throw;
            }
        }
    }
}

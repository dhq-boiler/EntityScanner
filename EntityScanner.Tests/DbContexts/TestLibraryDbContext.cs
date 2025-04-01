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

            // シードデータの内容を表示
            var bookSeedData = _entityScanner.GetSeedData<Book>().ToList();
            Console.WriteLine($"Book seed data count: {bookSeedData.Count}");
            foreach (var data in bookSeedData)
            {
                var dict = data as IDictionary<string, object>;
                if (dict != null)
                {
                    Console.WriteLine("Book seed data properties:");
                    foreach (var key in dict.Keys)
                    {
                        Console.WriteLine($"  {key}: {dict[key]}");
                    }
                }
            }

            // EntityScannerのシードデータをModelBuilderに適用
            _entityScanner.ApplyToModelBuilder(modelBuilder);
        }
    }
}

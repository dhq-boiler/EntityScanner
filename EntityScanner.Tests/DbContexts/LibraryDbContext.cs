using EntityScanner.Tests.Entities;
using Microsoft.EntityFrameworkCore;

namespace EntityScanner.Tests.DbContexts;

public class LibraryDbContext : DbContext
{
    // エンティティセット
    public DbSet<Member> Members { get; set; }
    public DbSet<MemberProfile> MemberProfiles { get; set; }
    public DbSet<Book> Books { get; set; }
    public DbSet<Category> Categories { get; set; }
    public DbSet<Publisher> Publishers { get; set; }
    public DbSet<Author> Authors { get; set; }
    public DbSet<BookAuthor> BookAuthors { get; set; }
    public DbSet<BorrowRecord> BorrowRecords { get; set; }
    public DbSet<MemberBookmark> MemberBookmarks { get; set; }
    public DbSet<Library> Libraries { get; set; }
    public DbSet<Shelf> Shelves { get; set; }
    public DbSet<BookInventory> BookInventories { get; set; }

    public LibraryDbContext(DbContextOptions<LibraryDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // 1対1の関係設定
        modelBuilder.Entity<Member>()
            .HasOne(m => m.Profile)
            .WithOne(p => p.Member)
            .HasForeignKey<MemberProfile>(p => p.MemberId);

        // 1対多の関係設定
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

        // 多対多の関係設定
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

        // 自己参照の関係
        modelBuilder.Entity<Category>()
            .HasOne(c => c.ParentCategory)
            .WithMany(c => c.SubCategories)
            .HasForeignKey(c => c.ParentCategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        // インデックスの設定
        modelBuilder.Entity<Book>()
            .HasIndex(b => b.ISBN)
            .IsUnique();

        modelBuilder.Entity<Member>()
            .HasIndex(m => m.Email)
            .IsUnique();

        modelBuilder.Entity<BookInventory>()
            .HasIndex(bi => new { bi.BookId, bi.CopyNumber })
            .IsUnique();
    }
}
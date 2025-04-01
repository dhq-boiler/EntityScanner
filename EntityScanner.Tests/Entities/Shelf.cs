using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EntityScanner.Tests.Entities;

// 本棚
public class Shelf
{
    [Key] public int Id { get; set; }

    [Required] [MaxLength(50)] public string ShelfCode { get; set; }

    [MaxLength(100)] public string Location { get; set; }

    // 多対1関係: 複数の棚は1つの図書館に属する
    [ForeignKey(nameof(Library))] public int LibraryId { get; set; }

    public Library Library { get; set; }

    // 1対多関係: 1つの棚には複数の本の在庫がある
    [InverseProperty(nameof(BookInventory.Shelf))]
    public List<BookInventory> BookInventories { get; set; } = new();
}
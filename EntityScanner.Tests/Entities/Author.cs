using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EntityScanner.Tests.Entities;

// 著者
public class Author
{
    [Key] public int Id { get; set; }

    [Required] [MaxLength(100)] public string Name { get; set; }

    [MaxLength(500)] public string Biography { get; set; }

    // 多対多関係: 著者は複数の本を持つことができる
    [InverseProperty(nameof(BookAuthor.Author))]
    public List<BookAuthor> Books { get; set; } = new();
}
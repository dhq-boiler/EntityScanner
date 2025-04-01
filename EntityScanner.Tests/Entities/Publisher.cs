using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EntityScanner.Tests.Entities;

// 出版社
public class Publisher
{
    [Key] public int Id { get; set; }

    [Required] [MaxLength(100)] public string Name { get; set; }

    [MaxLength(200)] public string Address { get; set; }

    // 1対多関係: 1つの出版社から複数の本が出版される
    [InverseProperty(nameof(Book.Publisher))]
    public List<Book> PublishedBooks { get; set; } = new();
}
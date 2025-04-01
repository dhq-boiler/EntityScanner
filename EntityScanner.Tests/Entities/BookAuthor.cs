using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EntityScanner.Tests.Entities;

// 多対多の中間テーブル: 本と著者の関連
public class BookAuthor
{
    [Key] public int Id { get; set; }

    [ForeignKey(nameof(Book))] public int BookId { get; set; }

    public Book Book { get; set; }

    [ForeignKey(nameof(Author))] public int AuthorId { get; set; }

    public Author Author { get; set; }

    // 著者の役割（例: 主著者、共著者、翻訳者など）
    [MaxLength(50)] public string Role { get; set; }
}
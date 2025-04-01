using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EntityScanner.Tests.Entities;

// 借り出し記録
public class BorrowRecord
{
    [Key] public int Id { get; set; }

    [ForeignKey(nameof(Member))] public int MemberId { get; set; }

    public Member Member { get; set; }

    [ForeignKey(nameof(Book))] public int BookId { get; set; }

    public Book Book { get; set; }

    public DateTime BorrowDate { get; set; }

    public DateTime DueDate { get; set; }

    public DateTime? ReturnDate { get; set; }

    public bool IsOverdue => ReturnDate == null && DateTime.Now > DueDate;
}
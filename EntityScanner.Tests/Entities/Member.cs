using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EntityScanner.Tests.Entities;

// 会員
public class Member
{
    [Key] public int Id { get; set; }

    [Required] [MaxLength(100)] public string Name { get; set; }

    [Required] [MaxLength(100)] public string Email { get; set; }

    public DateTime RegistrationDate { get; set; }

    // 1対1関係: 会員は1つのプロフィールを持つ
    public MemberProfile Profile { get; set; }

    // 1対多関係: 会員は複数の貸出記録を持つ
    [InverseProperty(nameof(BorrowRecord.Member))]
    public List<BorrowRecord> BorrowRecords { get; set; } = new();

    // 多対多関係: 会員は複数の本にブックマークを付けることができる
    [InverseProperty(nameof(MemberBookmark.Member))]
    public List<MemberBookmark> Bookmarks { get; set; } = new();
}
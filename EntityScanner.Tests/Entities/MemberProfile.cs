using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EntityScanner.Tests.Entities;

// 会員プロフィール (1対1関係: 会員に対して1つのプロフィール)
public class MemberProfile
{
    [Key] public int Id { get; set; }

    public string Address { get; set; }

    public string PhoneNumber { get; set; }

    public DateTime? DateOfBirth { get; set; }

    // 1対1関係の外部キー
    [ForeignKey(nameof(Member))] public int MemberId { get; set; }

    // 1対1のナビゲーションプロパティ
    public Member Member { get; set; }
}
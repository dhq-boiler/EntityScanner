using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EntityScanner.Tests.Entities
{
    // 多対多の中間テーブル: 会員と本のブックマーク
    public class MemberBookmark
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey(nameof(Member))]
        public int MemberId { get; set; }
        public Member Member { get; set; }

        [ForeignKey(nameof(Book))]
        public int BookId { get; set; }
        public Book Book { get; set; }

        public DateTime CreatedDate { get; set; }

        [MaxLength(500)]
        public string Notes { get; set; }
    }
}

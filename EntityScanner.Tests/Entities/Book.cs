using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EntityScanner.Tests.Entities
{
    // 本 
    public class Book
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(200)]
        public string Title { get; set; }

        [MaxLength(100)]
        public string Author { get; set; }

        public string ISBN { get; set; }

        public int PublicationYear { get; set; }

        // 多対1関係: 複数の本は1つのカテゴリに属する
        [ForeignKey(nameof(Category))]
        public int CategoryId { get; set; }
        public Category Category { get; set; }

        // 多対1関係: 複数の本は1つの出版社から出版される
        [ForeignKey(nameof(Publisher))]
        public int PublisherId { get; set; }
        public Publisher Publisher { get; set; }

        // 1対多関係: 1つの本には複数の貸出記録がある
        [InverseProperty(nameof(BorrowRecord.Book))]
        public List<BorrowRecord> BorrowRecords { get; set; } = new List<BorrowRecord>();

        // 多対多関係: 本は複数の著者を持つことができる
        [InverseProperty(nameof(BookAuthor.Book))]
        public List<BookAuthor> Authors { get; set; } = new List<BookAuthor>();

        // 多対多関係: 本は複数の会員にブックマークされる
        [InverseProperty(nameof(MemberBookmark.Book))]
        public List<MemberBookmark> BookmarkedBy { get; set; } = new List<MemberBookmark>();
    }
}

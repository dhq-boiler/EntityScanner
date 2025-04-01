using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EntityScanner.Tests.Entities
{
    // カテゴリ
    public class Category
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string Name { get; set; }

        [MaxLength(255)]
        public string Description { get; set; }

        // 1対多関係: 1つのカテゴリには複数の本が属する
        [InverseProperty(nameof(Book.Category))]
        public List<Book> Books { get; set; } = new List<Book>();

        // 階層構造を表現（自己参照関係）
        [ForeignKey(nameof(ParentCategory))]
        public int? ParentCategoryId { get; set; }
        public Category ParentCategory { get; set; }

        [InverseProperty(nameof(ParentCategory))]
        public List<Category> SubCategories { get; set; } = new List<Category>();
    }
}

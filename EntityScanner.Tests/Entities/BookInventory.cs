using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EntityScanner.Tests.Entities
{
    // 本の在庫
    public class BookInventory
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey(nameof(Book))]
        public int BookId { get; set; }
        public Book Book { get; set; }

        [ForeignKey(nameof(Shelf))]
        public int ShelfId { get; set; }
        public Shelf Shelf { get; set; }

        [Required]
        [MaxLength(50)]
        public string CopyNumber { get; set; }

        public enum StatusEnum
        {
            Available,
            OnLoan,
            Reserved,
            InMaintenance,
            Lost
        }

        [EnumDataType(typeof(StatusEnum))]
        public StatusEnum Status { get; set; }
    }
}

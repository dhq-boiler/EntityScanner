using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EntityScanner.Tests.Entities
{
    // 図書館
    public class Library
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string Name { get; set; }

        [MaxLength(200)]
        public string Address { get; set; }

        // 1対多関係: 1つの図書館には複数の棚がある
        [InverseProperty(nameof(Shelf.Library))]
        public List<Shelf> Shelves { get; set; } = new List<Shelf>();
    }
}

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SQLiteConcurrencyTokenIssue.Model
{
    public class TestRow
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long Id { get; set; }

        public string? Status { get; set; }

        [Timestamp]
        public byte[]? OpCounter { get; set; }
    }
}

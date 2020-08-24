using Microsoft.EntityFrameworkCore;

namespace SQLiteConcurrencyTokenIssue.Model
{
    public class TestSqliteContext : DbContext
    {
        public TestSqliteContext(DbContextOptions<TestSqliteContext> options) : base(options)
        {

        }

        public DbSet<TestRow> TestRows => Set<TestRow>();
    }
}

using com.github.akovac35.Logging.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NUnit.Framework;
using SQLiteConcurrencyTokenIssue.Model;
using System;
using System.Linq;

namespace SQLiteConcurrencyTokenIssue
{
    [TestFixture]
    public class Tests
    {
        [SetUp]
        public void Setup()
        {
        }

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            CustomOnWrite = writeContext =>
            {
                TestContext.WriteLine(writeContext);
            };

            CustomOnBeginScope = scopeContext =>
            {
                TestContext.WriteLine(scopeContext);
            };

            ServiceCollectionInstance = new ServiceCollection();
            ServiceCollectionInstance.AddTestLogger(CustomOnWrite, CustomOnBeginScope);
            ServiceCollectionInstance.AddSingleton<SqliteConnection>(fact =>
            {
                var connStr = "Filename=:memory:";
                var conn = new SqliteConnection(connStr);
                conn.Open();
                return conn;
            });
            ServiceCollectionInstance.AddSingleton<DbContextOptions<TestSqliteContext>>(fact =>
            {
                var conn = fact.GetRequiredService<SqliteConnection>();
                var loggingFact = fact.GetRequiredService<ILoggerFactory>();
                var tmp = new DbContextOptionsBuilder<TestSqliteContext>().UseSqlite(conn).UseLoggerFactory(loggingFact);
                tmp.EnableSensitiveDataLogging();
                return tmp.Options;
            });
            ServiceCollectionInstance.AddTransient<TestSqliteContext>(fact =>
            {
                var options = fact.GetRequiredService<DbContextOptions<TestSqliteContext>>();
                var tmp = new TestSqliteContext(options);
                return tmp;
            });

        }

        protected virtual IServiceCollection ServiceCollectionInstance { get; set; } = null!;

        protected virtual Action<WriteContext> CustomOnWrite { get; set; } = null!;
        protected virtual Action<ScopeContext> CustomOnBeginScope { get; set; } = null!;

        protected void InitializeTriggers(TestSqliteContext context)
        {
            var tables = context.Model.GetEntityTypes();

            foreach (var table in tables)
            {
                var props = table.GetProperties()
                                .Where(p => p.ClrType == typeof(byte[])
                                && p.ValueGenerated == Microsoft.EntityFrameworkCore.Metadata.ValueGenerated.OnAddOrUpdate
                                && p.IsConcurrencyToken);

                var tableName = table.GetTableName();

                foreach (var field in props)
                {
                    string[] SQLs = new string[] {
            $@"CREATE TRIGGER IF NOT EXISTS Set{tableName}_{field.Name}OnUpdate
			AFTER UPDATE ON [{tableName}] FOR EACH ROW
			BEGIN
				UPDATE [{tableName}]
				SET [{field.Name}] = randomblob(8)
				WHERE rowid = NEW.rowid;
			END
			",
            $@"CREATE TRIGGER IF NOT EXISTS Set{tableName}_{field.Name}OnInsert
			AFTER INSERT ON [{tableName}] FOR EACH ROW
			BEGIN
				UPDATE [{tableName}]
				SET [{field.Name}] = randomblob(8)
				WHERE rowid = NEW.rowid;
			END
			"
        };

                    foreach (var sql in SQLs)
                    {
                        using (var command = context.Database.GetDbConnection().CreateCommand())
                        {
                            command.CommandText = sql;
                            command.ExecuteNonQuery();
                        }
                    }
                }
            }
        }

        protected void CommonTestFunction(IServiceProvider serviceProvider, Action<TestSqliteContext> databaseInitializationAction)
        {
            long id = -1;
            byte[]? opCounter = null;

            using (var context = serviceProvider.GetRequiredService<TestSqliteContext>())
            {
                databaseInitializationAction(context);

                var tmp = new TestRow();
                tmp.Status = "Created";

                context.Add(tmp);
                context.SaveChanges();
                id = tmp.Id;
                opCounter = tmp.OpCounter;

                Assert.AreNotEqual(0, id);
                Assert.AreNotEqual(-1, id);
                Assert.AreNotEqual(null, opCounter);
            }

            using (var context = serviceProvider.GetRequiredService<TestSqliteContext>())
            {
                var tmp = context.TestRows.First(item => item.Id == id);
                context.Update(tmp);
                context.SaveChanges();

                Assert.AreNotEqual(opCounter, tmp.OpCounter);
            }
        }

        [Test]
        public void Works_With_Triggers()
        {
            var serviceProvider = ServiceCollectionInstance.BuildServiceProvider();
            CommonTestFunction(serviceProvider, context =>
            {
                context.Database.EnsureCreated();
                InitializeTriggers(context);
            });
        }

        [Test]
        public void Fails_Without_Triggers()
        {
            var serviceProvider = ServiceCollectionInstance.BuildServiceProvider();
            CommonTestFunction(serviceProvider, context =>
            {
                context.Database.EnsureCreated();
            });
        }
    }
}
using Microsoft.Data.Sqlite;

namespace HelloSqlite
{
    /// <summary>The database work, free of any UI so a test can call it.</summary>
    public static class SqliteDemo
    {
        /// <summary>Creates a table in memory, inserts a row and reads it back.</summary>
        public static string Run()
        {
            using (var connection = new SqliteConnection("Data Source=:memory:"))
            {
                connection.Open();

                using (SqliteCommand create = connection.CreateCommand())
                {
                    create.CommandText = "CREATE TABLE greeting (id INTEGER PRIMARY KEY, text TEXT NOT NULL)";
                    create.ExecuteNonQuery();
                }

                using (SqliteCommand insert = connection.CreateCommand())
                {
                    insert.CommandText = "INSERT INTO greeting (text) VALUES ($text)";
                    insert.Parameters.AddWithValue("$text", "Hello from SQLite");
                    insert.ExecuteNonQuery();
                }

                using (SqliteCommand select = connection.CreateCommand())
                {
                    select.CommandText = "SELECT text FROM greeting LIMIT 1";
                    return (string)select.ExecuteScalar() + " (SQLite " + connection.ServerVersion + ")";
                }
            }
        }
    }
}

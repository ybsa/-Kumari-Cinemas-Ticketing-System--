using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using System;
using System.IO;

namespace KumariCinemas.Web.Data
{
    public static class DbInitializer
    {
        public static void Initialize(IConfiguration configuration)
        {
            var connectionString = configuration.GetConnectionString("DefaultConnection");
            
            // Check if DB exists by trying to open connection. If file doesn't exist, SQLite will create it.
            using (var connection = new SqliteConnection(connectionString))
            {
                connection.Open();

                // Check if tables exist
                var command = connection.CreateCommand();
                command.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name='M_Users';";
                var tableName = command.ExecuteScalar() as string;

                if (string.IsNullOrEmpty(tableName))
                {
                    // Create Tables
                    CreateTables(connection);
                    // Seed Data
                    SeedData(connection);
                }
            }
        }

        private static void CreateTables(SqliteConnection connection)
        {
            var sql = @"
                CREATE TABLE M_Users (
                    UserId INTEGER PRIMARY KEY AUTOINCREMENT,
                    Username TEXT NOT NULL UNIQUE,
                    Password TEXT NOT NULL,
                    Address TEXT,
                    Role TEXT DEFAULT 'USER',
                    CreatedAt TEXT DEFAULT CURRENT_TIMESTAMP
                );

                CREATE TABLE M_Movies (
                    MovieId INTEGER PRIMARY KEY AUTOINCREMENT,
                    Title TEXT NOT NULL,
                    Duration TEXT, 
                    Language TEXT,
                    Genre TEXT,
                    ReleaseDate TEXT,
                    Description TEXT,
                    ImageUrl TEXT
                );

                CREATE TABLE M_Branches (
                    BranchId INTEGER PRIMARY KEY AUTOINCREMENT,
                    BranchName TEXT NOT NULL, 
                    Location TEXT
                );

                CREATE TABLE M_Halls (
                    HallId INTEGER PRIMARY KEY AUTOINCREMENT,
                    BranchId INTEGER,
                    HallName TEXT NOT NULL,
                    Capacity INTEGER NOT NULL,
                    FOREIGN KEY(BranchId) REFERENCES M_Branches(BranchId)
                );

                CREATE TABLE M_Shows (
                    ShowId INTEGER PRIMARY KEY AUTOINCREMENT,
                    MovieId INTEGER,
                    HallId INTEGER,
                    ShowDateTime TEXT NOT NULL,
                    BasePrice REAL NOT NULL,
                    FOREIGN KEY(MovieId) REFERENCES M_Movies(MovieId),
                    FOREIGN KEY(HallId) REFERENCES M_Halls(HallId)
                );

                CREATE TABLE T_Bookings (
                    BookingId INTEGER PRIMARY KEY AUTOINCREMENT,
                    UserId INTEGER,
                    ShowId INTEGER,
                    BookingTime TEXT DEFAULT CURRENT_TIMESTAMP,
                    Status TEXT DEFAULT 'BOOKED', 
                    FinalPrice REAL,
                    TotalTickets INTEGER DEFAULT 1,
                    FOREIGN KEY(UserId) REFERENCES M_Users(UserId),
                    FOREIGN KEY(ShowId) REFERENCES M_Shows(ShowId)
                );

                CREATE TABLE T_Reviews (
                    ReviewId INTEGER PRIMARY KEY AUTOINCREMENT,
                    MovieId INTEGER,
                    UserId INTEGER,
                    Rating REAL,
                    CommentText TEXT,
                    ReviewDate TEXT DEFAULT CURRENT_TIMESTAMP,
                    FOREIGN KEY(MovieId) REFERENCES M_Movies(MovieId),
                    FOREIGN KEY(UserId) REFERENCES M_Users(UserId)
                );

                CREATE TABLE T_Tickets (
                    TicketId INTEGER PRIMARY KEY AUTOINCREMENT,
                    BookingId INTEGER,
                    SeatNumber TEXT,
                    FOREIGN KEY(BookingId) REFERENCES T_Bookings(BookingId)
                );
            ";

            using (var command = connection.CreateCommand())
            {
                command.CommandText = sql;
                command.ExecuteNonQuery();
            }
        }

        private static void SeedData(SqliteConnection connection)
        {
            // Branches
            ExecuteSql(connection, "INSERT INTO M_Branches (BranchName, Location) VALUES ('Kumari Cinemas - Main', 'Downtown');");
            ExecuteSql(connection, "INSERT INTO M_Branches (BranchName, Location) VALUES ('Kumari Cinemas - East', 'Eastside');");

            // Halls
            ExecuteSql(connection, "INSERT INTO M_Halls (BranchId, HallName, Capacity) VALUES (1, 'Audi 1', 100);");
            ExecuteSql(connection, "INSERT INTO M_Halls (BranchId, HallName, Capacity) VALUES (1, 'Audi 2', 120);");
            ExecuteSql(connection, "INSERT INTO M_Halls (BranchId, HallName, Capacity) VALUES (2, 'Gold Class', 50);");

            // Movies
            ExecuteSql(connection, "INSERT INTO M_Movies (Title, Duration, Language, Genre, ReleaseDate, ImageUrl) VALUES ('Interstellar Voyage', '169 min', 'English', 'Fiction', datetime('now', '-10 days'), 'https://images.unsplash.com/photo-1419242902214-272b3f66ee7a?w=400&h=600&fit=crop');");
            ExecuteSql(connection, "INSERT INTO M_Movies (Title, Duration, Language, Genre, ReleaseDate, ImageUrl) VALUES ('Dragon Warriors', '142 min', 'English', 'Action', datetime('now', '-5 days'), 'https://images.unsplash.com/photo-1478720568477-152d9b164e26?w=400&h=600&fit=crop');");
            ExecuteSql(connection, "INSERT INTO M_Movies (Title, Duration, Language, Genre, ReleaseDate, ImageUrl) VALUES ('Laugh Out Loud', '98 min', 'English', 'Comedy', datetime('now', '-3 days'), 'https://images.unsplash.com/photo-1489599849927-2ee91cede3ba?w=400&h=600&fit=crop');");
            ExecuteSql(connection, "INSERT INTO M_Movies (Title, Duration, Language, Genre, ReleaseDate, ImageUrl) VALUES ('Midnight Mystery', '127 min', 'English', 'Horror', datetime('now', '+7 days'), 'https://images.unsplash.com/photo-1509347528160-9a9e33742cdb?w=400&h=600&fit=crop');");
            ExecuteSql(connection, "INSERT INTO M_Movies (Title, Duration, Language, Genre, ReleaseDate, ImageUrl) VALUES ('Love in Paris', '115 min', 'French', 'Drama', datetime('now', '+14 days'), 'https://images.unsplash.com/photo-1518676590629-3dcbd9c5a5c9?w=400&h=600&fit=crop');");

            // Shows (Next few days)
            ExecuteSql(connection, "INSERT INTO M_Shows (MovieId, HallId, ShowDateTime, BasePrice) VALUES (1, 1, datetime('now', '+1 day', '+10 hours'), 400);");
            ExecuteSql(connection, "INSERT INTO M_Shows (MovieId, HallId, ShowDateTime, BasePrice) VALUES (2, 2, datetime('now', '+1 day', '+14 hours'), 450);");
            ExecuteSql(connection, "INSERT INTO M_Shows (MovieId, HallId, ShowDateTime, BasePrice) VALUES (3, 3, datetime('now', '+2 days', '+18 hours'), 350);");

            // Admin User (Password: password -> hashed)
            // Note: Using the same hash as before, assuming the hash algorithm is compatible (likely SHA256 string). 
            ExecuteSql(connection, "INSERT INTO M_Users (Username, Password, Role) VALUES ('admin', '5e884898da28047151d0e56f8dc6292773603d0d6aabbdd62a11ef721d1542d8', 'ADMIN');");
        }

        private static void ExecuteSql(SqliteConnection connection, string sql)
        {
            using (var command = connection.CreateCommand())
            {
                command.CommandText = sql;
                command.ExecuteNonQuery();
            }
        }
    }
}

using SQLite;
using System;
using System.Collections.Generic;
using System.Text;

namespace TravelSystem.Shared.Factories;

public class SqliteConnectionFactory
{
    public SQLiteAsyncConnection CreateConnection(string dbPath)
    {

        return new SQLiteAsyncConnection(dbPath,
            SQLiteOpenFlags.ReadWrite |
            SQLiteOpenFlags.Create |
            SQLiteOpenFlags.SharedCache); 
    }
}

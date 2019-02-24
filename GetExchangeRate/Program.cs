using System;
using System.IO;
using System.Net;
using System.Data.SQLite;
using System.Json;
using System.Collections.Generic;
namespace GetExchangeRate
{
    class MainClass
    {
        const String URL = "https://api.exchangeratesapi.io/{0}?base=PLN";
        const String DB_FILE = "main.sqlite";

        /**
         * Definicja bazy danych
         */
        const String DB_DEF = @"
        CREATE TABLE currency(
            id integer NOT NULL PRIMARY KEY,
            name varchar(3) UNIQUE
            );

        CREATE TABLE exchange_rate(
            id integer NOT NULL PRIMARY KEY AUTOINCREMENT,
            currency integer NOT NULL,
            rate real,
            time text,

            FOREIGN KEY(currency) REFERENCES currency(id)
                ON UPDATE CASCADE
                ON DELETE CASCADE
        );

        CREATE TABLE comment(
            id integer NOT NULL PRIMARY KEY AUTOINCREMENT,
            content text,
            exchange_rate integer,

            FOREIGN KEY(exchange_rate) REFERENCES exchange_rate(id)
                ON UPDATE CASCADE
                ON DELETE CASCADE
        );

        CREATE INDEX currency_name_id ON currency(name, id);
        CREATE INDEX exchange_rate_id_currency_rate_time ON exchange_rate(id,currency,rate,time);
        ";

        /**
         * Inicjalizuje bazę danych - tworzy jej plik i wykorzystuje definicję
         */
        private static void initDB(string db_file_path)
        {
            SQLiteConnection.CreateFile(db_file_path);
            SQLiteConnection conn = new SQLiteConnection("Data Source=" + db_file_path + ";Version=3;");
            conn.Open();
            SQLiteCommand cmd = new SQLiteCommand(DB_DEF, conn);
            cmd.ExecuteNonQuery();
            conn.Close();
        }

        public static void Main(string[] args)
        {
            string db_file = String.Format("{0}\\..\\..\\..\\..\\{1}", AppContext.BaseDirectory, DB_FILE);
            if (!File.Exists(db_file))
            {
                Console.Out.WriteLine("Initializing the database file.");
                initDB(db_file);
            }

            String date;
            if (args.Length == 1)
            {
                date = args[0];
            }
            else
            {
                Console.Out.WriteLine("Requesting 'latest' currencies");
                date = "latest";
            }

            String url = String.Format(URL, date);

            SQLiteConnection conn = new SQLiteConnection("Data Source=main.sqlite;Version=3;");
            conn.Open();
            HttpWebRequest request = WebRequest.CreateHttp(url);
            request.Method = "GET";

            Console.Out.WriteLine("Performing GET request on " + url);
            var response = request.GetResponse();
            var responseString = new StreamReader(response.GetResponseStream()).ReadToEnd();
            Console.Out.WriteLine(responseString);
            JsonObject json = (JsonObject)JsonValue.Parse(responseString);

            Console.Out.WriteLine("Date: " + json["date"]);
            foreach (KeyValuePair<string, JsonValue> rate in json["rates"])
            {
                // Console.WriteLine(rate.Key + "=" + rate.Value.ToString());
                SQLiteCommand cmd = new SQLiteCommand(
                @"INSERT OR IGNORE INTO currency(name) VALUES(?);",
                conn
                );

                cmd.Parameters.AddWithValue(null, rate.Key);
                cmd.ExecuteNonQuery();

                cmd = new SQLiteCommand(
                @"INSERT OR IGNORE INTO exchange_rate(currency, rate, time)
                VALUES(
                    (SELECT c.id 
                     FROM currency as c 
                     WHERE c.name == ?), ?, ?
                );", conn);
                cmd.Parameters.AddWithValue(null, rate.Key);
                cmd.Parameters.AddWithValue(null, rate.Value);
                cmd.Parameters.AddWithValue(null, json["date"].ToString().Trim('"'));
                cmd.ExecuteNonQuery();
            }
            conn.Close();
        }
    }
}

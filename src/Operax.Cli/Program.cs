using System;
using System.IO;
using System.Data;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Dapper;

namespace Operax.Cli;

class Program
{
    private const string ConnectionString = "Server=BT-FIKRI\\SQLEXPRESS;Database=Operax;User Id=sa;Password=***REMOVED***;TrustServerCertificate=True";

    static async Task Main(string[] args)
    {
        Console.WriteLine("=== Operax DB Management Tool ===");

        if (args.Length == 0)
        {
            ShowHelp();
            return;
        }

        var command = args[0].ToLower();

        try
        {
            switch (command)
            {
                case "query":
                    if (args.Length < 2) { Console.WriteLine("Hata: Sorgu belirtilmedi."); return; }
                    await ExecuteQueryAsync(args[1]);
                    break;

                case "script":
                    if (args.Length < 2) { Console.WriteLine("Hata: Dosya yolu belirtilmedi."); return; }
                    await ExecuteScriptAsync(args[1]);
                    break;

                case "seed":
                    var seedPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "../../../../../tools/seed_complete.sql");
                    if (!File.Exists(seedPath)) 
                    {
                        seedPath = "d:\\Dev\\Operax\\tools\\seed_complete.sql";
                    }
                    await ExecuteScriptAsync(seedPath);
                    break;

                default:
                    Console.WriteLine($"Bilinmeyen komut: {command}");
                    ShowHelp();
                    break;
            }
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"\nHATA: {ex.Message}");
            if (ex.InnerException != null) Console.WriteLine($"DETAY: {ex.InnerException.Message}");
            Console.ResetColor();
        }
    }

    private static void ShowHelp()
    {
        Console.WriteLine("\nKullanim:");
        Console.WriteLine("  run-cli query \"SELECT ...\"     -> SQL sorgusu calistirir ve sonuclari basar.");
        Console.WriteLine("  run-cli script <dosya_yolu>     -> SQL dosyasini calistirir.");
        Console.WriteLine("  run-cli seed                    -> Varsayilan seed verilerini yukler.");
    }

    private static async Task ExecuteQueryAsync(string sql)
    {
        Console.WriteLine($"\nSorgu Calistiriliyor: {sql}");
        using var conn = new SqlConnection(ConnectionString);
        await conn.OpenAsync();

        var reader = await conn.ExecuteReaderAsync(sql);
        var table = new DataTable();
        table.Load(reader);

        if (table.Rows.Count == 0)
        {
            Console.WriteLine("Sonuc donmedi veya etkilenen satir yok.");
            return;
        }

        // Print columns
        foreach (DataColumn col in table.Columns) Console.Write($"{col.ColumnName}\t");
        Console.WriteLine("\n" + new string('-', 50));

        // Print rows
        foreach (DataRow row in table.Rows)
        {
            foreach (var item in row.ItemArray) Console.Write($"{item}\t");
            Console.WriteLine();
        }
        
        Console.WriteLine($"{table.Rows.Count} satir listelendi.");
    }

    private static async Task ExecuteScriptAsync(string path)
    {
        if (!File.Exists(path))
        {
            Console.WriteLine($"Hata: Dosya bulunamadi: {path}");
            return;
        }

        Console.WriteLine($"\nScript Calistiriliyor: {Path.GetFileName(path)}");
        var script = await File.ReadAllTextAsync(path);

        using var conn = new SqlConnection(ConnectionString);
        await conn.OpenAsync();

        // Split by GO if necessary, but Dapper can handle blocks
        // For complex scripts with GO, we might need a better parser, but let's try simple first
        var commands = script.Split(new[] { "\nGO\r", "\nGO\n", "\r\nGO\r\n" }, StringSplitOptions.RemoveEmptyEntries);

        foreach (var c in commands)
        {
            if (string.IsNullOrWhiteSpace(c)) continue;
            await conn.ExecuteAsync(c);
        }

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("Script basariyla tamamlandi.");
        Console.ResetColor();
    }
}

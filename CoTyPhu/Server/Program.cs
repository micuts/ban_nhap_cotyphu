using Server.Infrastructure.Network;
using Server.Infrastructure.Database.Connection; // Thêm dòng này để gọi DBConnection
using System;
using System.Threading.Tasks;

internal class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("--- KHOI DONG SERVER GAME CO TY PHU ---");

        // 1. Kiem tra ket noi Supabase truoc
        var db = new DBConnection();
        if (db.TestConnection())
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("[SUCCESS] Da ket noi voi Database tren Supabase.");
            Console.ResetColor();

            // 2. Neu ket noi DB thanh cong moi chay Server TCP
            Console.WriteLine("Server dang bat dau lang nghe tai cong 7777...");
            var server = new TcpServer(7777);
            await server.StartAsync();
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("[ERROR] Khong the ket noi Supabase. Vui long kiem tra Host/Password trong DBConnection.cs");
            Console.ResetColor();
            Console.WriteLine("Nhan phim bat ky de thoat...");
            Console.ReadKey();
        }
    }
}
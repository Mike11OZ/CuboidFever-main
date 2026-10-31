using System;

namespace CuboidFever;

public static class Program
{
    [STAThread]
    static void Main()
    {
        try
        {
            using var game = new CuboidFever.Game1();
            game.Run();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n[FATAL CRASH]:\n{ex}");
        }
        finally
        {
            // Game window has closed. The terminal remains open.
            Console.WriteLine("\n--- Game window closed. Terminal remains open. ---");
            
            // If you want the terminal to stay open until you manually close it, 
            // you can leave Console.ReadKey() here, or remove it if launched from an external shell.
            try 
            { 
                Console.ReadKey(); 
            } 
            catch 
            { }
        }
    }
}
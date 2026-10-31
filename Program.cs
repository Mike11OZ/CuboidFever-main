using System;

namespace CuboidFever;

public static class Program
{
    [STAThread]
    static void Main()
    {
        // Optional: You can call AllocConsole() here in Program.cs 
        // instead of inside Game1 so it opens before the game even loads.
        
        using var game = new CuboidFever.Game1();
        
        try
        {
            game.Run();
        }
        catch (Exception ex)
        {
            // If the game crashes, print the exception to your console window
            Console.WriteLine($"\n[FATAL CRASH]:\n{ex}");
        }
        finally
        {
            // This runs whether the game closed normally or crashed
            Console.WriteLine("\n--- Game process has terminated. Press any key to close this window ---");
            
            try 
            { 
                Console.ReadKey(); 
            } 
            catch 
            {
                // Handles edge cases where standard input isn't attached
            }
        }
    }
}
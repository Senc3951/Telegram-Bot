# Telegram-Bot
An extremely simple yet useful and easy-to-use Bot for Telegram using the Telegram API.
The bot uses asynchronous programming and will execute the pending tasks efficiently by using a Queue.

You can Send messages, locations, files, audio files, pictures.

# Set up
You need to create a bot using the BotFather in Telegram and get your API token.
Look at the code below, and paste your token in there.

Notice that the namespace is called TelegramRat because at first, I created this project to create a RAT, but the infrastructure for a bot is the same.

# Example
```
using System;
using System.Threading.Tasks;

namespace TelegramRat
{
    internal class Program
    {
        private static readonly NetworkManager networkManager = new NetworkManager("YOUR_BOT_TOKEN");
        
        private static void Main(string[] args)
        {
            networkManager.OnReceived = OnReceived;
            networkManager.OnError = OnError;
            networkManager.Start();
            Console.WriteLine("Bot is live and running.");

            while (true) { }
        }

        private static void OnReceived(string username, string message)
        {
            if (string.IsNullOrWhiteSpace(message))
                return;
            
            //Using a Task because we can't have the OnReceived method to be asynchronous.
            Task.Factory.StartNew(async () =>
            {
                Console.WriteLine($"{message} from {username}");
                if (message == "/disconnect")
                    await Disconnect();
                    
                networkManager.SendMessageAsync($"Received your message: {message}");
            });
        }
        
        private static void OnError(Exception exception)
        {
            Console.WriteLine(exception.Message);
        }

        private static async Task Disconnect()
        {
            await networkManager.StopAsync();
            networkManager.Dispose();

            Environment.Exit(0);
        }
    }
}
```

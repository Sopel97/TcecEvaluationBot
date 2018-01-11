﻿namespace TcecEvaluationBot.ConsoleUI
{
    using System;
    using System.Collections.Generic;

    using TcecEvaluationBot.ConsoleUI.Commands;

    using TwitchLib;
    using TwitchLib.Models.Client;

    public class TwitchBot
    {
        private readonly TwitchClient twitchClient;

        private readonly IList<(string Text, ICommand Command)> commands = new List<(string, ICommand)>();

        public TwitchBot(Options options)
        {
            var credentials = new ConnectionCredentials(options.TwitchUserName, options.TwitchAccessToken);
            this.twitchClient = new TwitchClient(credentials, options.TwitchChannelName);
            this.commands.Add(("eval", new EvalCommand(this.twitchClient, options)));
            this.commands.Add(("time", new TimeCommand(this.twitchClient, options)));
        }

        public void Run()
        {
            this.twitchClient.OnConnected += (sender, arguments) => this.Log("Connected!");
            this.twitchClient.OnJoinedChannel += (sender, arguments) => this.Log($"Joined to {arguments.Channel}!");
            this.twitchClient.OnMessageReceived += (sender, arguments) =>
                {
                    foreach (var command in this.commands)
                    {
                        if (arguments.ChatMessage.Message == $"!{command.Text}"
                            || arguments.ChatMessage.Message.Trim().StartsWith($"!{command.Text} "))
                        {
                            this.Log($"Received \"{arguments.ChatMessage.Message}\" from {arguments.ChatMessage.Username}");
                            var response = command.Command.Execute(arguments.ChatMessage.Message);
                            this.twitchClient.SendMessage(response);
                            this.Log($"Responded with \"{response}\"");
                        }
                    }
                };
            this.twitchClient.Connect();
        }

        private void Log(string message)
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write($"[{DateTime.UtcNow}]");
            Console.ResetColor();
            Console.WriteLine($" {message}");
        }
    }
}
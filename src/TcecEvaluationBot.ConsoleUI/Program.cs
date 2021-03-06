﻿namespace TcecEvaluationBot.ConsoleUI
{
    using System;
    using System.Globalization;
    using System.Text;
    using System.Threading;

    using CommandLine;

    using TcecEvaluationBot.ConsoleUI.Settings;

    public static class Program
    {
        public static void Main(string[] args)
        {
            Console.OutputEncoding = Encoding.Unicode;
            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;

            var settingsParser = new SettingsParser();
            var settings = settingsParser.ParseSettings("appsettings.json");

            var parserResult = Parser.Default.ParseArguments<Options>(args);
            parserResult.WithParsed(options => RunBot(options, settings));
        }

        private static void RunBot(Options options, Settings.Settings settings)
        {
            using var bot = new TwitchBot(options, settings);
            bot.OutputMovesTask();
            bot.Run();
            Console.ReadLine();
        }
    }
}

﻿namespace TcecEvaluationBot.ConsoleUI.Commands
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Net.Http;
    using System.Threading.Tasks;

    using TcecEvaluationBot.ConsoleUI.Services;

    using TwitchLib;

    public class EvaluationCommand : ICommand
    {
        private readonly TwitchClient twitchClient;

        private readonly Options options;

        private readonly string[] availableEngines = { "komodo", "stockfish", "laser", "ginkgo" };

        private readonly IPositionEvaluator stockfishPositionEvaluator;
        private readonly IPositionEvaluator komodoPositionEvaluator;
        private readonly IPositionEvaluator laserPositionEvaluator;
        private readonly IPositionEvaluator ginkgoPositionEvaluator;

        private readonly HttpClient httpClient;

        public EvaluationCommand(TwitchClient twitchClient, Options options)
        {
            this.twitchClient = twitchClient;
            this.options = options;
            this.stockfishPositionEvaluator = new UciEnginePositionEvaluator(options, "stockfish.exe", "SF_120218");
            this.komodoPositionEvaluator = new UciEnginePositionEvaluator(options, "komodo.exe", "Komodo_11.2.2, Courtesy of K authors");
            this.laserPositionEvaluator = new UciEnginePositionEvaluator(options, "laser.exe", "Laser_1.5");
            this.ginkgoPositionEvaluator = new UciEnginePositionEvaluator(options, "ginkgo.exe", "Ginkgo_2.03, Courtesy of G authors");
            this.httpClient = new HttpClient();
        }

        public string Execute(string message)
        {
            var engine = "stockfish"; // TODO: Add default engine to options
            var moveTime = this.options.DefaultEvaluationTime * 1000;
            var commandParts = message.Split(new[] { " " }, StringSplitOptions.RemoveEmptyEntries);
            if (commandParts.Length > 1)
            {
                for (var i = 1; i < commandParts.Length; i++)
                {
                    if (int.TryParse(commandParts[i], out var moveTimeArgument) && moveTimeArgument >= this.options.MinEvaluationTime
                                                                                && moveTimeArgument <= this.options.MaxEvaluationTime)
                    {
                        moveTime = moveTimeArgument * 1000;
                    }
                    else if (this.availableEngines.Contains(commandParts[i].ToLower().Trim()))
                    {
                        engine = commandParts[i].ToLower().Trim();
                    }
                }
            }

            if (this.options.ThinkingMessage)
            {
                this.twitchClient.SendMessage($"[{DateTime.UtcNow:HH:mm:ss}] Thinking {moveTime / 1000} sec., please wait.");
            }

            var evaluation = this.Evaluate(moveTime, engine);
            return evaluation;
        }

        private string Evaluate(int moveTime, string engine)
        {
            var livePgnAsString = this.GetTextContent("http://tcec.chessdom.com/live/live.pgn").GetAwaiter().GetResult();
            var fenPosition = this.ConvertPgnToFen(livePgnAsString);
            if (fenPosition == null)
            {
                Console.WriteLine("Invalid fen! See if file.pgn contains a valid PGN.");
                return null;
            }

            string evaluationMessage;
            switch (engine.ToLower().Trim())
            {
                case "komodo":
                    evaluationMessage = this.komodoPositionEvaluator.GetEvaluation(fenPosition, moveTime);
                    break;
                case "laser":
                    evaluationMessage = this.laserPositionEvaluator.GetEvaluation(fenPosition, moveTime);
                    break;
                case "ginkgo":
                    evaluationMessage = this.ginkgoPositionEvaluator.GetEvaluation(fenPosition, moveTime);
                    break;
                default:
                    evaluationMessage = this.stockfishPositionEvaluator.GetEvaluation(fenPosition, moveTime);
                    break;
            }

            return evaluationMessage;
        }

        private string ConvertPgnToFen(string livePgnAsString)
        {
            File.WriteAllText("file.pgn", livePgnAsString);
            var process = new Process
                              {
                                  StartInfo = new ProcessStartInfo
                                                  {
                                                      FileName = "pgn-extract.exe",
                                                      Arguments = "-F file.pgn",
                                                      UseShellExecute = false,
                                                      RedirectStandardOutput = true,
                                                      CreateNoWindow = true,
                                                  }
                              };
            process.Start();

            var lastMeaningfulLine = string.Empty;
            while (!process.StandardOutput.EndOfStream)
            {
                var currentLine = process.StandardOutput.ReadLine();
                if (currentLine != string.Empty && currentLine != "*")
                {
                    lastMeaningfulLine = currentLine;
                    //// Console.WriteLine(lastMeaningfulLine);
                }
            }

            var outputParts = lastMeaningfulLine?.Split("\"");
            string fenPosition = null;
            if (outputParts?.Length > 2)
            {
                fenPosition = outputParts[1];
                //// Console.WriteLine(fenPosition);
            }

            return fenPosition;
        }

        private async Task<string> GetTextContent(string url)
        {
            var response = await this.httpClient.GetAsync($"{url}?noCache={Guid.NewGuid()}");
            var stringResult = await response.Content.ReadAsStringAsync();
            return stringResult;
        }
    }
}

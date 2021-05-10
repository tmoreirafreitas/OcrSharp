using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.IO;

namespace TesseractApi.Services
{
    public class TesseractService
    {
        private readonly ILogger<TesseractService> logger;

        public TesseractService(ILogger<TesseractService> logger)
        {
            this.logger = logger;
        }

        public string DecodeFile(string inputFileName, string tesseractCommand)
        {
            if (!File.Exists(inputFileName))
                throw new FileNotFoundException("Input file does not exists", inputFileName);

            //Tesseract adiciona sozinho o .txt no nome do arquivo de output.
            var outputFileNameWithoutExtension = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("D"));

            var outputFileName = $"{outputFileNameWithoutExtension}.txt";

            string returnValue = null;

            try
            {
                this.ExecuteTesseractProcess(string.Format(tesseractCommand, $"{inputFileName} {outputFileNameWithoutExtension}"));

                if (File.Exists(outputFileName))
                {
                    returnValue = File.ReadAllText(outputFileName);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex.ToString());
                throw;
            }
            finally
            {
                File.Delete(outputFileName);
            }

            return returnValue;
        }

        private int ExecuteTesseractProcess(string args)
        {
            var tesseractCreateInfo = new ProcessStartInfo("tesseract", args)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            var tesseractProcess = Process.Start(tesseractCreateInfo);

            string output = tesseractProcess.StandardOutput.ReadToEnd();
            string error = tesseractProcess.StandardError.ReadToEnd();

            tesseractProcess.WaitForExit(1000 * 30);


            if (tesseractProcess.ExitCode != 0)
            {
                logger.LogError($"Executed Process: {tesseractCreateInfo.FileName}");
                logger.LogError($"Args: {tesseractCreateInfo.Arguments}");
                logger.LogError($"Output: {output}");
                logger.LogError($"Error: {error}");

                throw new InvalidOperationException($"Error on execute {tesseractCreateInfo.FileName} with args '{tesseractCreateInfo.Arguments}', exit code {tesseractProcess.ExitCode}");
            }
            else
            {
                logger.LogInformation($"Executed Process: {tesseractCreateInfo.FileName}");
                logger.LogInformation($"Args: {tesseractCreateInfo.Arguments}");
                logger.LogInformation($"Output: {output}");
                logger.LogInformation($"Error: {error}");
            }

            return tesseractProcess.ExitCode;
        }
    }
}
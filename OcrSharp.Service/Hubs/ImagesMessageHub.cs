using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using OcrSharp.Domain;
using OcrSharp.Domain.Entities;
using OcrSharp.Domain.Interfaces.Hubs;
using OcrSharp.Domain.Interfaces.Services;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace OcrSharp.Service.Hubs
{
    public class ImagesMessageHub : Hub<IStreaming>
    {
        private readonly IDocumentFileService _documentService;
        private readonly ILogger _logger;

        public ImagesMessageHub(IDocumentFileService documentService, ILoggerFactory loggerFactory)
        {
            _documentService = documentService;
            _logger = loggerFactory.CreateLogger<ImagesMessageHub>();
        }

        public async Task ExtractTextFromPdf(PdfData file, Accuracy accuracy, string user)
        {
            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();

            string mensagem;
            if (!Path.GetExtension(file.FileName).Equals(".pdf"))
            {
                mensagem = $@"Há extensão de arquivo não suportado, o tipo suportado é: Arquivos adobe PDF(*.pdf)";
                _logger.LogInformation($"Enviando mensagem: {mensagem}");
                await Clients.Client(user).ImageMessage(mensagem, StatusMensagem.FALHA);
            }

            var fileMemory = new InMemoryFile()
            {
                FileName = file.FileName,
                Content = file.Binary,
            };

            var resultDocument = await _documentService.ExtractTextFromPdf(user, fileMemory, accuracy);

            _logger.LogInformation($"Enviando arquivo processado: {fileMemory.FileName} para o client: {user}");
            string jsonData = string.Format("{0}\n", JsonConvert.SerializeObject(resultDocument));
            await Clients.Client(user).ImageMessage(jsonData, StatusMensagem.TEXTO_EXTRAIDO);

            stopWatch.Stop();
            TimeSpan ts = stopWatch.Elapsed;
            string elapsedTime = string.Format("{0:00}:{1:00}:{2:00}.{3:00}",
            ts.Hours, ts.Minutes, ts.Seconds,
            ts.Milliseconds / 10);

            mensagem = $"Tempo total de processamento: {elapsedTime}";
            _logger.LogInformation($"Enviando mensagem: {mensagem}");
            await Clients.Client(user).ImageMessage(mensagem, StatusMensagem.FINALIZADO);
        }
    }
}

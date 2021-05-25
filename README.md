[![Docker Image CI](https://github.com/tmoreirafreitas/OcrSharp/actions/workflows/docker-image.yml/badge.svg)](https://github.com/tmoreirafreitas/OcrSharp/actions/workflows/docker-image.yml)

# OCR SHARP API (Tesseract + EmguCV + .NET5)
<div style="text-align:justify; text-justify:inter-word;">
<p>É uma aplicação Web API utilizada para extrair textos a partir de uma imagem JPG, PNG, TIFF, e também de documentos PDF.</p>
<p>Também é útil para ser reaproveitada em outros contextos.</p> 
<p>A implementação utiliza o programa tesseract instalado na imagem docker para extrair o texto da imagem.</p>
<p>Forma de utilização facilitada, vem com swagger para facilitar o entendimento das chamadas das funcionalidades desejadas.</p>
</div>

## EmguCV
<div style="text-align:justify; text-justify:inter-word;">
<p>É uma wrapper cross plataforma .NET para a biblioteca OpenCV de código aberto para a visão computacional, processamento de imagem e aprendizagem de máquina, com aceleração de GPU para operação em tempo real.</p>
<p>Pode ser compilado pelo Visual Studio e Unity e pode ser executado no Windows, Linux, Mac OS, iOS e Android.</p>
</div>

## Tesseract
Tesseract é uma plataforma de reconhecimento ótico de caracteres de código aberto sob a Licença Apache 2.0, foi desenvolvido pela Hewlett-Packard e foi por um tempo mantido pelo Google; atualmente o projeto está hospedado no [GitHub](https://github.com/tesseract-ocr/tesseract).

## Como Implantar
1. Baixe a imagem Docker na sua máquina:
```Powershell
docker pull tmoreirafreitas/tesseract-ocrapi:latest
```

2. Execute o Docker na sua máquina:
```Powershell
docker run -it -p 9090:80 --name tesseract-ocrapi -d tmoreirafreitas/tesseract-ocrapi
```
Ou
```Powershell
docker run -it -p 9090:80 --name tesseract-ocrapi -e Oem="--oem 3" -e Psm="--psm 6" -e Dpi="--dpi 300" -e Language="por" -d tmoreirafreitas/tesseract-ocrapi
```

3. Acesse a URL do Swagger: http://localhost:9090/swagger

## Como Usar
<div style="text-align:justify; text-justify:inter-word;">
<p>A extração do texto é feita executando um serviço REST. É necessário fazer a requisição por meio de um método POST com cabeçalho de multipart, preenchendo o campo file do form com o conteúdo binário da imagem/documento.</p>
<p>A extração do texto de um arquivo PDF é feito através do serviço Hub do SignalR. 
O Hub do SignalR é acessado pela URL: <a href="http://localhost:9090/OcrMessageHub" rel="nofollow">http://localhost:9090/OcrMessageHub</a>
</p>
<p>O arquivo é enviado para o Hub conforme o contrato: <code>{
  "binary": "string",
  "fileName": "string",
  "headers": "string"
}</code> para o Método <strong>ExtractTextFromPdf</strong></p>
<p>O resultado da extração do texto é recebido ouvindo o método <strong>OcrMessage</strong> do Hub.</p>

<p>Abaixo segue um exemplo de uso em C#, não se preocupe existe cliente SignalR para diversas outras linguagens de programação como: Java, JavaScript, Python, etc. E você não terá dificuldades em adaptar o exemplo para sua linguagem preferida.</p>
<pre lang='cs'>
<code>
class Program
{
	private static HubConnection _connection;
	private static ILogger _logger;
        static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args);
        static async Task Main(string[] args)
        {
            var cancellationTokenSource = new CancellationTokenSource();
            using IHost host = CreateHostBuilder(args)
                .ConfigureLogging(options => options.AddLog4Net("log4net.config"))
                .Build();

            await Process(host.Services, cancellationTokenSource.Token);

            await host.RunAsync();
        }

        static async Task Process(IServiceProvider services, CancellationToken token)
        {
            using var serviceScope = services.CreateScope();
            var provider = serviceScope.ServiceProvider;
            _configuration = provider.GetRequiredService<IConfiguration>();
            _logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger<Program>();

            var filename = $@"{_configuration["FileConfiguration:Filenames"]}";

            _connection = new HubConnectionBuilder()
                .WithUrl($@"http://localhost:9090/OcrMessageHub")
                .WithAutomaticReconnect()
                .AddMessagePackProtocol()
                .Build();

            _connection.Closed += async (error) =>
            {
                if (error == null)
                    _logger.LogError("Connection closed without error.");
                else
                    _logger.LogError($"Connection closed due to an error: {error}");

                await Task.Delay(new Random().Next(0, 5) * 1000);
                await _connection.StartAsync(token);
            };

            _connection.Reconnected += (connectionId) =>
            {
                _logger.LogInformation($"Connection successfully reconnected. The ConnectionId is now: {connectionId}");
                return Task.CompletedTask;
            };

            _connection.Reconnecting += (exception) =>
            {
                _logger.LogInformation($"Connection started reconnecting due to an error: {exception}");
                return Task.CompletedTask;
            };

            _connection.On<string, StatusMessage>("OcrMessage", (message, status) =>
            {
                switch (status)
                {
                    case StatusMessage.FAILURE:
                        _logger.LogError($"STATUS: {status}, Message: {message}");
                        return;
                    case StatusMessage.EXTRACTED_TEXT:
                        var document = JsonConvert.DeserializeObject<DocumentFile>(message);
                        var filename = $"{Path.GetFileNameWithoutExtension(document.FileName)}.txt";
                        var file = File.Create(filename);

                        var sb = new StringBuilder();
                        foreach (var page in document.Pages)
                            sb.AppendLine(page.Content);

                        byte[] data = Encoding.UTF8.GetBytes(sb.ToString());
                        file.WriteAsync(data, 0, data.Length, token).Wait();
                        file.Flush();
                        file.Dispose();

                        _logger.LogInformation($"Arquivo processado e disponibilizado em {filename}");
                        break;
                }
            });

            await _connection.StartAsync(token);

            using var cancelAfterDelay = new CancellationTokenSource();
            using var fs = File.OpenRead(filename);
            using var ms = new MemoryStream();
            await fs.CopyToAsync(ms);
            var pdfData = new PdfData { FileName = Path.GetFileName(filename), Binary = ms.ToArray(), Headers = "data:application/pdf;base64" };
            try
            {
                _logger.LogInformation($"Enviando mensagem: {pdfData.FileName}");
                await _connection.InvokeAsync("ExtractTextFromPdf", pdfData, Accuracy.Low, _connection.ConnectionId, cancelAfterDelay.Token);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Erro ao tentar enviar mensagem: {pdfData.FileName}");
                _logger.LogError(ex.Message);
            }
        }
}
</code>
</pre>

<p> Analisando o código acima há dois pontos de atenção:
<ol>
	<li>Na chamada do método <pre lang='cs'><code>_connection.On<string, StatusMessage>("OcrMessage", (message, status) =>{})</code></pre> é retornado além da mensagem JSON que é o resultado da extração do arquivo representado pela variável [message], é também retornado o status da operação que podem ser:
		<ul>
			<li><strong>FAILURE:</strong> É status de erro que possa ocorrer no momento da extração na API.</li>
			<li><strong>EXTRACTED_TEXT:</strong> É status de sucesso com o arquivo processado na variável [message].</li>
		</ul>
	</li>
	<li>
		<strong>Accuracy:</strong> <p>É um enum com os seguintes valores: Low = 0, Medium = 1, Hight = 2. Esses parâmentros servem para que API possa escolher o arquivo traineddata das mais alta acurácia porém mais lento ao mais rápido porém com uma acurácia não tão boa.</p>
		<p>Para mais informações consulte: <a href="https://tesseract-ocr.github.io/tessdoc/#traineddata-files" rel="nofollow">Traineddata Files</a></p>
	</li>
</ol>
</p>
</div>

## Configuração da extração de texto
<div style="text-align:justify; text-justify:inter-word;">
<p>Esta imagem está utilizando o tesseract versão 4, configurada para extrair conteúdos escritos em português e/ou inglês. Também foi adicionada uma propriedade para que o processo do tesseract rodasse paralelamente, aumentando o poder de performance para arquivo PDF com múltiplas páginas e para que múltiplas requisições de arquivo de imagem fossem atentidas.</p>
<p>Neste momento a aplicação faz uso do EmguCV para tratamento de suavização e distorção na imagem enviada.</p>
<p>Como podemos vê acima no item 3 da sessão <strong>Como implantar</strong> percebemos que existem algumas variáveis de ambiente que é de suma importância para o perfeito funcionamento do tesseract, vamos a elas: 
<ol>
	<li><strong>Oem:</strong> Tesseract tem vários modos de motor com desempenho e velocidade diferentes. O Tesseract 4 introduziu o modo de rede neural LSTM adicional, que geralmente funciona melhor.
		<ul>
			<li>Padrão: -e Oem="--oem 3"</li>
		</ul>
	</li>
	<li><strong>Psm:</strong> Isso afeta como o Tesseract divide a imagem em linhas de texto e palavras. Escolha aquele que funciona melhor para você.
		<ul>
			<li>Padrão: -e Psm="--psm 6"</li>
		</ul>
	</li>
	<li><strong>Dpi:</strong> É Definição da qualidade da imagem de entrada para o tesseract.
		<ul>
			<li>Padrão: -e Dpi="--dpi 300"</li>
		</ul>
	</li>
	<li><strong>Language:</strong> É o idioma nativo que o Tesseract usará ao fazer o ORC da imagem. Vários idiomas podem ser solicitados usando: -e Language="por+eng"
		<ul>
			<li>Padrão: -e Language="por"</li>
		</ul>
	</li>
</ol>

</p>
</div>


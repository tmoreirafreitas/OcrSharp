# OcrSharp (Tesseract + EmguCV + .NET5) [![Docker Image CI](https://github.com/tmoreirafreitas/OcrSharp/actions/workflows/docker-image.yml/badge.svg)](https://github.com/tmoreirafreitas/OcrSharp/actions/workflows/docker-image.yml)

É uma aplicação Web API que é útil para ser reaproveitada em outros contextos. Forma de utilização facilitada, vem com swagger para facilitar o entendimento das chamadas das funcionalidades desejadas.

As funcionalidades são:

* Obter o texto de um arquivo (pdf).
* Obter o texto de uma página do arquivo (pdf).
* Converter uma página em imagem (tiff).
* Converter o arquivo (pdf) em múltiplas imagens (tiff).
* Obter os dados OCR(texto e porcentagem de confiabilidade) de uma imagem.
* Obter o texto OCR de uma imagem.

## EmguCV
É uma wrapper cross plataforma .NET para a biblioteca OpenCV de código aberto para a visão computacional, processamento de imagem e aprendizagem de máquina, com aceleração de GPU para operação em tempo real.
Pode ser compilado pelo Visual Studio e Unity e pode ser executado no Windows, Linux, Mac OS, iOS e Android.

## Tesseract
Tesseract é uma plataforma de reconhecimento ótico de caracteres de código aberto sob a Licença Apache 2.0, foi desenvolvido pela Hewlett-Packard e foi por um tempo mantido pelo Google; atualmente o projeto está hospedado no [GitHub](https://github.com/tesseract-ocr/tesseract).

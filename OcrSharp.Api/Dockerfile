#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:5.0 AS base
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/sdk:5.0 AS build
WORKDIR /src
COPY ["OcrSharp.Api/OcrSharp.Api.csproj", "OcrSharp.Api/"]
COPY ["OcrSharp.Infra.CrossCutting.IoC/OcrSharp.Infra.CrossCutting.IoC.csproj", "OcrSharp.Infra.CrossCutting.IoC/"]
COPY ["OcrSharp.Service/OcrSharp.Service.csproj", "OcrSharp.Service/"]
COPY ["OcrSharp.Domain/OcrSharp.Domain.csproj", "OcrSharp.Domain/"]
RUN dotnet restore "OcrSharp.Api/OcrSharp.Api.csproj"
COPY . .
WORKDIR "/src/OcrSharp.Api"
RUN dotnet build "OcrSharp.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "OcrSharp.Api.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "OcrSharp.Api.dll"]
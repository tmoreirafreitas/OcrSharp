FROM mcr.microsoft.com/dotnet/aspnet:5.0-focal AS base
LABEL version="1.0"
LABEL author="Thiago Gabriel Moreira Freitas"
LABEL description="Ubuntu-Focal with Emgucv and dotnet-5.0"
WORKDIR /app
WORKDIR /

# Update Packages
RUN apt-get update && apt-get -y install --no-install-recommends \
    build-essential \
    apt-transport-https \
    software-properties-common \
    wget \
    unzip \
    ca-certificates \
    x264 \
    libgtk-3-dev \
    libgstreamer1.0-dev \
    libavcodec-dev \
    libswscale-dev \
    libavformat-dev \
    libdc1394-22-dev \
    libv4l-dev \
    cmake-curses-gui \
    ocl-icd-dev \
    freeglut3-dev \
    libgeotiff-dev \
    libusb-1.0-0-dev \
    cmake \
    git \
    gfortran \
    nano \
    automake \
    libtool \
    libc6-dev \
    libgdiplus \
    apt-utils \
    libleptonica-dev -y\
    tesseract-ocr \
    libtesseract-dev -y\
    && apt-get -y clean \
    && rm -rf /var/lib/apt/lists/*

RUN ln -s /usr/lib/libgdiplus.so/usr/lib/gdiplus.dll
RUN ln -s /usr/lib/x86_64-linux-gnu/liblept.so.5 libleptonica-1.80.0.so
RUN ln -s /usr/lib/x86_64-linux-gnu/libtesseract.so.4.0.1 libtesseract41.so
RUN mkdir /app/x64
RUN cp /usr/lib/x86_64-linux-gnu/liblept.so.5 /app/x64/libleptonica-1.80.0.so
RUN cp /usr/lib/x86_64-linux-gnu/libtesseract.so.4.0.1 /app/x64/libtesseract41.so

WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/sdk:5.0-focal AS build
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

RUN cp /app/liblinuxemgucv/libcvextern.so /usr/lib

ENTRYPOINT ["dotnet", "OcrSharp.Api.dll"]
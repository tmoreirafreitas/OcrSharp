FROM ubuntu:20.04
ENV LC_ALL=C.UTF-8 LANG=C.UTF-8
LABEL version="1.0"
LABEL author="Thiago Gabriel Moreira Freitas"
LABEL description="Ubunto-20.04 with EmguCV and dotnet-5"

RUN apt-get update; \
    apt-get install -y wget

RUN wget https://packages.microsoft.com/config/ubuntu/20.04/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
RUN dpkg -i packages-microsoft-prod.deb

# Install SDK dotnet
RUN apt-get update; \
    apt-get install -y apt-transport-https && \
    apt-get update && \
    apt-get install -y dotnet-sdk-5.0

#Install runtime dotnet
RUN apt-get update; \
    apt-get install -y apt-transport-https && \
    apt-get update && \
    apt-get install -y dotnet-runtime-5.0

#Install git
RUN apt-get update; \
    apt-get install -y git gfortran

RUN apt-get update -qq && apt-get install -y libc6-dev libgdiplus apt-utils
RUN ln -s /usr/lib/libgdiplus.so/usr/lib/gdiplus.dll

# Make sure all emgu dependencies are in place
# http://www.emgu.com/wiki/index.php/Download_And_Installation#Getting_ready
WORKDIR /mnt/emgu_repo
RUN git clone https://github.com/emgucv/emgucv emgucv
WORKDIR /mnt/emgu_repo/emgucv
RUN git submodule update --init --recursive
WORKDIR /mnt/emgu_repo/emgucv/platforms/ubuntu/20.04
RUN chmod +x apt_install_dependency.sh
RUN chmod +x cmake_configure.sh
RUN ./apt_install_dependency.sh
RUN ./cmake_configure.sh


#Install nano
RUN apt-get update && apt-get install -y nano

WORKDIR /app

WORKDIR /src
COPY ["OcrSharp.Api/OcrSharp.Api.csproj", "OcrSharp.Api/"]
COPY ["OcrSharp.Infra.CrossCutting.IoC/OcrSharp.Infra.CrossCutting.IoC.csproj", "OcrSharp.Infra.CrossCutting.IoC/"]
COPY ["OcrSharp.Service/OcrSharp.Service.csproj", "OcrSharp.Service/"]
COPY ["OcrSharp.Domain/OcrSharp.Domain.csproj", "OcrSharp.Domain/"]
RUN dotnet restore "OcrSharp.Api/OcrSharp.Api.csproj"
COPY . .

WORKDIR "/src/OcrSharp.Api"
RUN mkdir /app/build
RUN dotnet build "OcrSharp.Api.csproj" -c Release -o /app/build
RUN dotnet publish "OcrSharp.Api.csproj" -c Release -o /app
RUN rm -r /app/build

WORKDIR /app
EXPOSE 80
ENV ASPNETCORE_URLS http://*:80

ENTRYPOINT ["dotnet", "OcrSharp.Api.dll"]
# ENTRYPOINT ["bash"]
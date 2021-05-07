#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.
# author
MAINTAINER Thiago Gabriel Moreira Freitas

# extra metadata
LABEL version="1.0"
LABEL description="First image with Dockerfile."

#FROM mcr.microsoft.com/dotnet/aspnet:5.0 AS base
FROM mcr.microsoft.com/dotnet/aspnet:5.0-focal AS base
WORKDIR /app
EXPOSE 80

#FROM mcr.microsoft.com/dotnet/sdk:5.0 AS build
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
#run as www-data(33), readonly
RUN chown -R 33:33 ./
RUN chmod -R 0500 ./
RUN chmod -R 0700 *.dll #if the dll's are not writeable, it won't start

# Update Packages
RUN apt-get update -qq && apt-get install -y libc6-dev libgdiplus apt-utils
RUN ln -s /usr/lib/libgdiplus.so/usr/lib/gdiplus.dll
RUN apt-get -y install build-essential libgtk-3-dev libgstreamer1.0-dev libavcodec-dev libswscale-dev 
RUN apt-get -y install libavformat-dev libdc1394-22-dev libv4l-dev cmake-curses-gui ocl-icd-dev 
RUN apt-get -y install freeglut3-dev libgeotiff-dev libusb-1.0-0-dev
RUN apt-get -y install checkinstall cmake pkg-config yasm
RUN apt-get -y install git gfortran

#RUN apt-get -y install libjpeg8-dev libjpeg-dev libpng-dev
#RUN apt-get -y install libjasper1
#RUN apt-get -y install libtiff-dev
#RUN apt-get -y install libavcodec-dev libavformat-dev libswscale-dev libdc1394-22-dev
#RUN apt-get -y install libxine2-dev libv4l-dev
#RUN apt-get -y install libgstreamer1.0-dev libgstreamer-plugins-base1.0-dev
#RUN apt-get -y install libgtk2.0-dev libtbb-dev qt5-default
#RUN apt-get -y install libatlas-base-dev
#RUN apt-get -y install libfaac-dev libmp3lame-dev libtheora-dev
#RUN apt-get -y install libvorbis-dev libxvidcore-dev
#RUN apt-get -y install libopencore-amrnb-dev libopencore-amrwb-dev
#RUN apt-get -y install libavresample-dev
#RUN apt-get -y install x264 v4l-utils
#RUN apt-get -y install libprotobuf-dev protobuf-compiler
#RUN apt-get -y install libgoogle-glog-dev libgflags-dev
#RUN apt-get -y install libgphoto2-dev libeigen3-dev libhdf5-dev doxygen
#RUN apt-get -y install libtbb2 libjasper-dev libdc1394-22-dev

RUN git clone https://github.com/emgucv/emgucv emgucv
WORKDIR /app/emgucv
RUN git submodule update --init --recursive
WORKDIR /app/emgucv/platforms/ubuntu/20.04
RUN chmod +x apt_install_dependency.sh
RUN chmod +x cmake_configure.sh
RUN ./apt_install_dependency.sh
RUN ./cmake_configure.sh

RUN apt-get install -y nano
RUN export LD_LIBRARY_PATH=.:$LD_LIBRARY_PATH
ENTRYPOINT ["dotnet", "OcrSharp.Api.dll"]

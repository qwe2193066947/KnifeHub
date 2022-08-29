#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS base
WORKDIR /app
EXPOSE 80

# start Selenium
# 注意: 以下 为了使用 Selenium
# 复制 attachments/selenium/ 文件夹下的文件到 ./attachments/selenium/ 即 /app/attachments/selenium/
COPY attachments/selenium/ ./attachments/selenium/
RUN apt update
RUN apt-get install unzip
RUN apt-get install gdebi-core -y
WORKDIR /app/attachments/selenium
# 安装 Chrome
# 注意: Chrome 版本必须与 chromedriver 版本对应
# 安装 google-chrome*.deb 包依赖
# 注意: gdebi 没有 -y, 使用 -n 代替
RUN gdebi -n google-chrome*.deb
# 安装 chromedriver
RUN unzip chromedriver_linux64.zip
RUN mv chromedriver /app/chromedriver
WORKDIR /app
# 为所有用户添加可执行权限 (对chromedriver文件)
RUN chmod a+x chromedriver
# TODO: 以下添加 PATH 失败
RUN echo 'export PATH=$PATH:/app' >> ~/.bash_profile
RUN /bin/bash -c "source ~/.bash_profile"
# 使用 Dockerfile 方式 添加 PATH
ENV PATH=/app:$PATH
# 效验版本
RUN google-chrome --version
RUN chromedriver --version
# 下面两行安装中文字体
RUN apt install -y --force-yes --no-install-recommends fonts-wqy-microhei
RUN apt install -y --force-yes --no-install-recommends ttf-wqy-zenhei
# 以下安装 Selenium WebDriver 需要的依赖
RUN apt-get install libglib2.0 -y
RUN apt-get install libnss3-dev -y
RUN apt-get install libxcb1 -y
# end Selenium

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src
COPY ["src/QQBotHub.Web/QQBotHub.Web.csproj", "src/QQBotHub.Web/"]
COPY ["src/QQBotHub.Sdk/QQBotHub.Sdk.csproj", "src/QQBotHub.Sdk/"]
RUN dotnet restore "src/QQBotHub.Web/QQBotHub.Web.csproj"
COPY . .
WORKDIR "/src/src/QQBotHub.Web"
RUN dotnet build "QQBotHub.Web.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "QQBotHub.Web.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "QQBotHub.Web.dll"]

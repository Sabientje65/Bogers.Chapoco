FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base

FROM build AS build

WORKDIR /src
COPY . .
RUN dotnet restore
RUN dotnet build -c Release -o /app

# prepare mitmproxym 
# should set version in env var
# should use another working directory
# should see if we can skip writing to fs first, https://unix.stackexchange.com/a/85195
RUN apt-get update \
    && apt-get install wget \
    && wget https://downloads.mitmproxy.org/10.2.4/mitmproxy-10.2.4-linux-x86_64.tar.gz \
    && tar -xvf mitmproxy-10.2.4-linux-x86_64.tar.gz

# move build artifacts to app
FROM base
WORKDIR /app
COPY --from=build ./app .
COPY --from=build ./src/mitmdump .    

ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
VOLUME /app/appsettings.json
VOLUME /flows

ENTRYPOINT ["bash"]
# ENTRYPOINT ["dotnet", "Bogers.Chapoco.Api.dll"]

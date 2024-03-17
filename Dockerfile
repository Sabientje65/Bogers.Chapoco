FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base

FROM build

WORKDIR /src
COPY . .
RUN dotnet restore
RUN dotnet build -c Release -o /app

FROM base
WORKDIR /app
COPY --from=build . .

ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

ENTRYPOINT ["bash"]
# ENTRYPOINT ["dotnet", "Bogers.Chapoco.Api.dll"]

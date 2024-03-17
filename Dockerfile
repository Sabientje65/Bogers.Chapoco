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


#USER $APP_UID
#WORKDIR /app
#EXPOSE 8080
#EXPOSE 8081
#
#
#
#WORKDIR /src
#COPY ["Bogers.Chapoco.Api/Bogers.Chapoco.Api.csproj", "Bogers.Chapoco.Api/"]
#RUN dotnet restore "Bogers.Chapoco.Api/Bogers.Chapoco.Api.csproj"
#COPY . .
#WORKDIR "/src/Bogers.Chapoco.Api"
#RUN dotnet build "Bogers.Chapoco.Api.csproj" -c $BUILD_CONFIGURATION -o /app/build
#
#FROM build AS publish
#ARG BUILD_CONFIGURATION=Release
#RUN dotnet publish "Bogers.Chapoco.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false
#
#FROM base AS final
#WORKDIR /app
#COPY --from=publish /app/publish .
#ENTRYPOINT ["dotnet", "Bogers.Chapoco.Api.dll"]

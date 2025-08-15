FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/Services/ClickerGame.Players/ClickerGame.Players.csproj", "src/Services/ClickerGame.Players/"]
COPY ["src/BuildingBlocks/ClickerGame.Shared/ClickerGame.Shared.csproj", "src/BuildingBlocks/ClickerGame.Shared/"]
RUN dotnet restore "src/Services/ClickerGame.Players/ClickerGame.Players.csproj"
COPY . .
WORKDIR "/src/src/Services/ClickerGame.Players"
RUN dotnet build "ClickerGame.Players.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "ClickerGame.Players.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "ClickerGame.Players.dll"]
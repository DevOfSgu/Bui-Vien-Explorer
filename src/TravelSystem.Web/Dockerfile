FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY TravelSystem.sln ./
COPY src/TravelSystem.Web/TravelSystem.Web.csproj src/TravelSystem.Web/
COPY src/TravelSystem.Shared/TravelSystem.Shared.csproj src/TravelSystem.Shared/
RUN dotnet restore src/TravelSystem.Web/TravelSystem.Web.csproj

COPY . .
RUN dotnet publish src/TravelSystem.Web/TravelSystem.Web.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
ENTRYPOINT ["dotnet", "TravelSystem.Web.dll"]

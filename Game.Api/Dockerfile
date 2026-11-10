FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Game.Api.csproj ./
RUN dotnet restore Game.Api.csproj
COPY . ./
RUN dotnet publish Game.Api.csproj --configuration Release --output /app/publish --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080
COPY --from=build /app/publish ./
USER app
EXPOSE 8080
ENTRYPOINT ["dotnet", "Game.Api.dll"]

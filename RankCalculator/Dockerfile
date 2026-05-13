FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["RankCalculator.csproj", "./"]
RUN dotnet restore "RankCalculator.csproj"
COPY . .
RUN dotnet publish "RankCalculator.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/runtime:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "RankCalculator.dll"]

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["EventsLogger.csproj", "./"]
RUN dotnet restore "EventsLogger.csproj"
COPY . .
RUN dotnet publish "EventsLogger.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/runtime:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "EventsLogger.dll"]
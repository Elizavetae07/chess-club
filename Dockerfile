# Используем официальный образ .NET SDK для сборки
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /app

# Копируем csproj и восстанавливаем зависимости
COPY *.csproj ./
RUN dotnet restore

# Копируем всё остальное и собираем
COPY . ./
RUN dotnet publish -c Release -o out

# Финальный образ — только runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app/out ./

# Render сам задаёт порт через переменную PORT
ENV ASPNETCORE_URLS=http://0.0.0.0:10000
EXPOSE 10000

ENTRYPOINT ["dotnet", "ChessClubApi.dll"]

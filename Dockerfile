# Этап 1: сборка
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Копируем csproj и восстанавливаем зависимости
COPY ChessClubApi.csproj ./
RUN dotnet restore

# Копируем всё, что нужно для сборки
COPY Program.cs ./
COPY appsettings.json ./
COPY appsettings.Development.json ./

# Публикуем
RUN dotnet publish ChessClubApi.csproj -c Release -o /app/out

# Этап 2: runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app

# Копируем собранное приложение
COPY --from=build /app/out ./

# ВАЖНО: копируем index.html и students.json в рабочую папку
COPY index.html ./
COPY students.json ./

ENV ASPNETCORE_URLS=http://0.0.0.0:10000
EXPOSE 10000

ENTRYPOINT ["dotnet", "ChessClubApi.dll"]

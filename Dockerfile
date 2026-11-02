# ExamR API — multi-stage image for Render (or any Docker host).
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY src/ExamFlow.Parsing/ExamFlow.Parsing.csproj src/ExamFlow.Parsing/
COPY src/ExamFlow.Api/ExamFlow.Api.csproj src/ExamFlow.Api/
RUN dotnet restore src/ExamFlow.Api/ExamFlow.Api.csproj
COPY src/ src/
RUN dotnet publish src/ExamFlow.Api/ExamFlow.Api.csproj -c Release -o /app --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_RUNNING_IN_CONTAINER=true
# Non-root user shipped with the .NET images.
USER $APP_UID
# Render injects PORT; listen on all interfaces. Falls back to 8080 elsewhere.
ENTRYPOINT ["sh", "-c", "ASPNETCORE_URLS=http://0.0.0.0:${PORT:-8080} exec dotnet ExamFlow.Api.dll"]

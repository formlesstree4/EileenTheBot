FROM mcr.microsoft.com/dotnet/sdk:6.0-alpine AS build
WORKDIR /source

# copy and publish app and libraries
COPY . .
RUN dotnet restore -r linux-musl-x64
RUN dotnet publish source/Bot -c release -o /app -r linux-musl-x64 /p:PublishReadyToRun=true

# final stage/image
FROM mcr.microsoft.com/dotnet/runtime:6.0-alpine
WORKDIR /app
COPY --from=build /app .

ENV RavenIP ""
ENV LogLevel ""

EXPOSE 5000

WORKDIR /app
ENTRYPOINT ["dotnet", "Bot.dll"]

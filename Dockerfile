FROM mcr.microsoft.com/dotnet/sdk:6.0-alpine AS build
WORKDIR /source

# copy and publish app and libraries
COPY . .
RUN dotnet restore -r linux-musl-x64
RUN dotnet publish source/Bot -c release -o /app -r linux-musl-x64 --self-contained true /p:PublishReadyToRun=true

# final stage/image
FROM mcr.microsoft.com/dotnet/aspnet:6.0-alpine AS base
WORKDIR /app
RUN apk add --no-cache icu-libs krb5-libs libgcc libintl libssl1.1 libstdc++ zlib
COPY --from=build /app .

ENV RavenIP ""
ENV LogLevel ""
ENV DOTNET_RUNNING_IN_CONTAINER=true
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false

EXPOSE 5000

WORKDIR /app
ENTRYPOINT ["dotnet", "Bot.dll"]

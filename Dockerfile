# syntax=docker/dockerfile:1
FROM mcr.microsoft.com/dotnet/sdk:11.0-preview AS build
WORKDIR /src

# Restore against the Warden build/package props and the project.
COPY Warden/global.json Warden/Directory.Build.props Warden/Directory.Packages.props Warden/
COPY Warden/src/Warden/Warden.csproj Warden/src/Warden/
RUN cd Warden && dotnet restore src/Warden/Warden.csproj

# Bring in the source and your status page content, then publish.
COPY Warden/src/Warden/ Warden/src/Warden/
COPY content/ content/
RUN cd Warden && dotnet publish src/Warden/Warden.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:11.0-preview AS runtime
WORKDIR /app

# only needed for the opt-in Git:Enabled content sync (see GitContentSyncService)
RUN apt-get update && apt-get install -y --no-install-recommends git \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app ./

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "Warden.dll"]

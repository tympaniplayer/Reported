FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG TARGETARCH
WORKDIR /src

COPY Reported.sln ./
COPY Reported/Reported.csproj Reported/
COPY Reported.Persistence/Reported.Persistence.csproj Reported.Persistence/
COPY Reported.Tests/Reported.Tests.csproj Reported.Tests/
RUN dotnet restore -a $TARGETARCH

COPY . .

FROM build AS test
RUN dotnet test --configuration Release --no-restore --verbosity normal

FROM test AS publish
RUN dotnet publish Reported/Reported.csproj -a $TARGETARCH -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/runtime:9.0 AS final
RUN groupadd -r reported && useradd -r -g reported reported
RUN mkdir -p /data && chown reported:reported /data
WORKDIR /app
COPY --from=publish /app/publish .
USER reported
ENV DATABASE_PATH=/data/reported.db
ENTRYPOINT ["dotnet", "Reported.dll"]

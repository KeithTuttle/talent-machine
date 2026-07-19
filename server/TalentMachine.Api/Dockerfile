FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY TalentMachine.Api.csproj ./
RUN dotnet restore TalentMachine.Api.csproj

COPY . .
RUN dotnet publish TalentMachine.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app .

ENV ASPNETCORE_ENVIRONMENT=Production
ENTRYPOINT ["dotnet", "TalentMachine.Api.dll"]

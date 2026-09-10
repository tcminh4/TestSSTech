FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY src/PartnerTransactions.Api/PartnerTransactions.Api.csproj src/PartnerTransactions.Api/
RUN dotnet restore src/PartnerTransactions.Api/PartnerTransactions.Api.csproj
COPY src/PartnerTransactions.Api/ src/PartnerTransactions.Api/
RUN dotnet publish src/PartnerTransactions.Api/PartnerTransactions.Api.csproj -c Release -o /app/publish --no-restore

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Development
ENTRYPOINT ["dotnet", "PartnerTransactions.Api.dll"]

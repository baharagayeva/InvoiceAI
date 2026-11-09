# 1-ci mərhələ: kompilyasiya
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Əvvəl yalnız csproj: paketlər dəyişməyibsə, Docker restore addımını keşdən götürür
COPY InvoiceAI.Api/InvoiceAI.Api.csproj InvoiceAI.Api/
RUN dotnet restore InvoiceAI.Api/InvoiceAI.Api.csproj

COPY InvoiceAI.Api/ InvoiceAI.Api/
RUN dotnet publish InvoiceAI.Api/InvoiceAI.Api.csproj -c Release -o /app --no-restore

# 2-ci mərhələ: yalnız runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "InvoiceAI.Api.dll"]
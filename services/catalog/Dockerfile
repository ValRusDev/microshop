# build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore services/catalog/MicroShop.Services.Catalog.csproj
RUN dotnet publish services/catalog/MicroShop.Services.Catalog.csproj -c Release -o /out /p:UseAppHost=false

# runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /out .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet","MicroShop.Services.Catalog.dll"]

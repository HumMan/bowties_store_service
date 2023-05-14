FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build-env
WORKDIR /app

# Copy csproj and restore as distinct layers
COPY *.csproj ./
RUN dotnet restore

# Copy everything else and build
COPY . ./
RUN dotnet publish -c Release -o out

# Build runtime image
FROM mcr.microsoft.com/dotnet/aspnet:6.0
WORKDIR /app
COPY --from=build-env /app/out .
# Custom CA cert for yandex cloud (splitted by 2 files)
# https://storage.yandexcloud.net/cloud-certs/CA.pem
COPY yandex_ca/* /usr/local/share/ca-certificates/
RUN update-ca-certificates
ENTRYPOINT ["dotnet", "store_service.dll"]


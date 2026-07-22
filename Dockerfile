# Build
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore (copy project files first for layer caching)
COPY src/Pho.Domain/Pho.Domain.csproj src/Pho.Domain/
COPY src/Pho.Infrastructure/Pho.Infrastructure.csproj src/Pho.Infrastructure/
COPY src/Pho.Web/Pho.Web.csproj src/Pho.Web/
RUN dotnet restore src/Pho.Web/Pho.Web.csproj

# Publish
COPY . .
RUN dotnet publish src/Pho.Web/Pho.Web.csproj -c Release -o /app --no-restore

# Runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .

# Let the app's Kestrel config bind both ports (avoid the base image's default URL).
ENV ASPNETCORE_URLS=""
ENV Pho__AdminPort=8080
ENV Pho__MockPort=8081
ENV ConnectionStrings__Pho="Data Source=/data/pho.db"

EXPOSE 8080
EXPOSE 8081
VOLUME /data

ENTRYPOINT ["dotnet", "Pho.Web.dll"]

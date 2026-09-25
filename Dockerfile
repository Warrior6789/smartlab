# Build SmartLab.API (.NET 8)
# docker build -t smartlab-api .
# docker run -p 8080:8080 \
#   -e ConnectionStrings__DefaultConnection="Host=...;Database=...;Username=...;Password=...;SSL Mode=Require" \
#   -e Jwt__SecretKey=... \
#   -e SeedAdmin__Email=... -e SeedAdmin__Password=... \
#   smartlab-api

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Restore first (cached layer as long as project files don't change)
COPY SmartLab.sln ./
COPY SmartLab.API/SmartLab.API.csproj SmartLab.API/
COPY SmartLab.BLL/SmartLab.BLL.csproj SmartLab.BLL/
COPY SmartLab.DAL/SmartLab.DAL.csproj SmartLab.DAL/
RUN dotnet restore SmartLab.API/SmartLab.API.csproj

COPY SmartLab.API/ SmartLab.API/
COPY SmartLab.BLL/ SmartLab.BLL/
COPY SmartLab.DAL/ SmartLab.DAL/
RUN dotnet publish SmartLab.API/SmartLab.API.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

ENV ASPNETCORE_HTTP_PORTS=8080

COPY --from=build /app/publish .
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "SmartLab.API.dll"]

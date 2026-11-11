# Imagen única: la API y el panel salen del mismo contenedor.
# La base es SQLite dentro del volumen /datos: no hace falta levantar un MySQL
# para una lavandería de barrio, y hacer backup es copiar un archivo.

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS api
WORKDIR /src
COPY LavAR.sln ./
COPY src/Lavar.Core/Lavar.Core.csproj src/Lavar.Core/
COPY src/Lavar.Api/Lavar.Api.csproj src/Lavar.Api/
RUN dotnet restore src/Lavar.Api/Lavar.Api.csproj
COPY src/ src/
RUN dotnet publish src/Lavar.Api/Lavar.Api.csproj -c Release -o /publicado /p:UseAppHost=false

FROM node:22-alpine AS web
WORKDIR /web
COPY web/package.json web/package-lock.json ./
RUN npm ci
COPY web/ ./
RUN npm run build

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app

COPY --from=api /publicado ./
# El panel se sirve como archivos estáticos desde wwwroot, en el mismo origen.
COPY --from=web /web/dist ./wwwroot

RUN mkdir -p /datos
ENV ASPNETCORE_URLS=http://+:8080 \
    ConnectionStrings__Lavar="Data Source=/datos/lavar.db"
EXPOSE 8080

ENTRYPOINT ["dotnet", "Lavar.Api.dll"]

# * Tahap build: image SDK berisi compiler, dipakai hanya untuk menghasilkan file publish
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# * csproj disalin dan di-restore lebih dulu supaya layer paket NuGet di-cache selama dependensi tidak berubah
COPY src/UptimeMonitor.Web/UptimeMonitor.Web.csproj src/UptimeMonitor.Web/
RUN dotnet restore src/UptimeMonitor.Web/UptimeMonitor.Web.csproj

COPY src/ src/
RUN dotnet publish src/UptimeMonitor.Web/UptimeMonitor.Web.csproj -c Release -o /app/publish --no-restore

# * Tahap runtime: image aspnet jauh lebih kecil karena hanya berisi runtime, tanpa SDK
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app

# ! Folder kunci dibuat dan dimiliki user "app" sebelum USER diganti, supaya volume yang dipasang ke sini bisa ditulis
RUN mkdir -p /app/keys && chown $APP_UID /app/keys

COPY --from=build /app/publish .

# * Menjalankan aplikasi sebagai user biasa (bukan root); Kestrel di image ini listen di port 8080
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "UptimeMonitor.Web.dll"]

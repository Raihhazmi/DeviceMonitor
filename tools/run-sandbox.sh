#!/bin/bash
# Menjalankan dashboard di sandbox dengan SQLite (SQL Server tidak tersedia di sini).
# Di Windows/produksi: gunakan Monitor:Provider=SqlServer (lihat docs/DEPLOYMENT.md).
export DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1
export NUGET_PACKAGES=/opt/nuget
export PATH=/opt/dotnet:$PATH
export Monitor__Provider=Sqlite
export Monitor__AgentApiKey=DEMO-KEY-12345
export ASPNETCORE_URLS=http://127.0.0.1:5080
cd /tmp/dm-app || exit 1
exec /opt/dotnet/dotnet DeviceMonitor.Web.dll

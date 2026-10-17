# Panduan Deployment — Device Monitoring Dashboard

Dokumen ini memandu instalasi lengkap: database SQL Server, aplikasi web, dan agent collector
di perangkat kantor.

---

## 1. Persiapan database SQL Server

### Opsi A — instance yang sudah ada (produksi)

1. Buat database:

   ```sql
   CREATE DATABASE DeviceMonitor;
   GO
   ```

2. Buat login/service account khusus (jangan pakai `sa` untuk aplikasi):

   ```sql
   CREATE LOGIN svc_devicemonitor WITH PASSWORD = 'Ganti-Dengan-Password-Kuat!';
   CREATE USER  svc_devicemonitor FOR LOGIN svc_devicemonitor;
   ALTER ROLE db_ddladmin ADD MEMBER svc_devicemonitor;   -- dibutuhkan saat migration otomatis
   ALTER ROLE db_datareader ADD MEMBER svc_devicemonitor;
   ALTER ROLE db_datawriter ADD MEMBER svc_devicemonitor;
   GO
   ```

   > Setelah migration pertama berhasil, peran `db_ddladmin` boleh dicabut.

3. Skema dibuat otomatis oleh aplikasi saat start (`Monitor:AutoMigrate = true`).
   Bila DBA ingin menjalankan skrip manual:

   ```bash
   sqlcmd -S server -d DeviceMonitor -U svc_devicemonitor -i db/migration.sql
   ```

   lalu set `Monitor:AutoMigrate = false` di `appsettings.json`.
   Skrip `db/migration.sql` bersifat **idempotent** (aman dijalankan berulang).

### Opsi B — Docker untuk development

```bash
docker compose -f db/docker-compose.yml up -d
```

Connection string bawaan sudah menunjuk ke instance ini
(`Server=localhost,1433;User Id=sa;Password=Your_Strong_Pass123!`).

---

## 2. Deploy aplikasi web

### 2.1 Publish

```bash
dotnet publish src/DeviceMonitor.Web -c Release -o C:\Apps\DeviceMonitor
```

### 2.2 Konfigurasi (`appsettings.json` di folder publish)

```jsonc
{
  "ConnectionStrings": {
    "SqlServerDb": "Server=SQLKANTOR,1433;Database=DeviceMonitor;User Id=svc_devicemonitor;Password=...;TrustServerCertificate=True;Encrypt=True"
  },
  "Monitor": {
    "Provider": "SqlServer",
    "AgentApiKey": "BUAT-STRING-ACAK-64-KARAKTER",
    "AutoMigrate": true,
    "Seed": { "OnStartup": false }          // matikan seed demo di produksi
  }
}
```

Buat API key acak, contoh PowerShell:

```powershell
-join ((48..57) + (65..90) + (97..122) | Get-Random -Count 48 | ForEach-Object {[char]$_})
```

### 2.3 Menjalankan sebagai Windows Service (disarankan)

```powershell
sc.exe create DeviceMonitorWeb binpath= "C:\Apps\DeviceMonitor\DeviceMonitor.Web.exe" start= auto
sc.exe description DeviceMonitorWeb "Dashboard monitoring perangkat kantor"
net start DeviceMonitorWeb
```

Hosting sebagai service membutuhkan Kestrel URL dikonfigurasi, mis. lewat environment variable:

```powershell
setx /M ASPNETCORE_URLS "http://0.0.0.0:5000"
```

### 2.4 Alternatif: IIS (in-process)

1. Install **Hosting Bundle .NET 8** di server IIS.
2. Buat site/application menunjuk `C:\Apps\DeviceMonitor`, Application Pool = *No Managed Code*.
3. Beri izin modify pada folder bila memakai SQLite (tidak diperlukan untuk SQL Server).

### 2.5 HTTPS / reverse proxy

- IIS: bind sertifikat internal, aktifkan redirect HTTP→HTTPS.
- Nginx/Caddy di depan Kestrel juga bisa; pastikan header `X-Forwarded-*` diteruskan
  (`app.UseForwardedHeaders()` bila diperlukan).

### 2.6 Autentikasi (opsional tapi disarankan)

Dashboard default terbuka untuk jaringan internal. Untuk membatasi akses:

- **Windows Authentication**: aktifkan di IIS + `services.AddAuthentication(NegotiateDefaults...)`.
- **Entra ID / OIDC**: `AddOpenIdConnect` pada `Program.cs`.
- Minimal: batasi firewall agar hanya subnet IT yang bisa mengakses port dashboard.

---

## 3. Deploy agent collector

### 3.1 Publish agent

```bash
dotnet publish agent/DeviceMonitor.Agent -c Release -r win-x64 --self-contained false -o C:\Apps\DeviceMonitorAgent
```

### 3.2 Konfigurasi agent (`appsettings.json`)

```jsonc
{
  "Agent": {
    "ServerUrl": "https://monitor.kantor.local",
    "ApiKey": "SAMAKAN-DENGAN-Monitor:AgentApiKey",
    "IntervalMinutes": 360,
    "SoftwareEveryCycles": 4,
    "Department": null,     // isi manual bila tidak terbaca dari AD
    "Location": null
  }
}
```

### 3.3 Install cepat (per PC)

Jalankan sebagai Administrator:

```powershell
powershell -ExecutionPolicy Bypass -File tools\install-agent.ps1 `
   -ServerUrl "https://monitor.kantor.local" -ApiKey "..." 
```

Skrip akan:
1. menyalin hasil publish ke `C:\DeviceMonitor`,
2. mendaftar sebagai **Windows Service** bernama `DeviceMonitorAgent` (auto start),
3. membuat rule firewall outbound bila diperlukan.

Bila kebijakan kantor melarang service pihak ketiga, gunakan mode scheduled task:

```powershell
tools\install-agent.ps1 -Mode ScheduledTask -ServerUrl ... -ApiKey ...
```

### 3.4 Distribusi massal (GPO / Intune / SCCM)

- **GPO Computer Configuration → Software Installation / Startup Script**: arahkan ke
  `install-agent.ps1` di sysvol/share.
- **Intune**: bungkus sebagai Win32 app dengan detection rule folder `C:\DeviceMonitor\DeviceMonitor.Agent.exe`.
- Pastikan share tempat agent berada bisa diakses akun SYSTEM.

### 3.5 Verifikasi

- Event Viewer → Application and Service Logs → *DeviceMonitorAgent* (log via EventLog provider).
- Atau file log bila dijalankan console: `DeviceMonitor.Agent.exe` langsung.
- Di dashboard: halaman **04 Agent Health** → log run muncul tiap interval; hostname baru
  otomatis tampil di Overview begitu payload pertama masuk.
- Antrian offline tersimpan di `%ProgramData%\DeviceMonitor\queue` dan terkirim otomatis
  saat server pulih.

---

## 4. Migrasi data awal dari tools inventaris lama

1. Export dari PDQ/Lansweeper/SCCM/Excel menjadi CSV dengan kolom sesuai template
   (tombol *Template* di halaman **Data & Ingest**).
2. Import urutan: `devices` → `disks` → `software` → (opsional) `snapshots`, `agentruns`.
3. Klik **Hitung ulang compliance** setelah mengatur aturan di bagian bawah halaman.

---

## 5. Operasional harian

| Tugas | Lokasi |
|-------|--------|
| Melihat fleet & perangkat offline | 01 Executive Overview |
| Perencanaan upgrade RAM / evaluasi model | 02 Hardware |
| Membersihkan disk kritis | 03 Storage (panel partisi C & Top 10 lowest) |
| Menindak agent gagal / antrian menumpuk | 04 Agent Health (filter status Failed/Queued) |
| Menindak software dilarang / versi tua | 05 Software (panel temuan compliance) |
| Menambah aturan compliance | Data & Ingest → tabel aturan |
| Backup | backup database SQL Server standar (FULL/LOG sesuai kebijakan) |

## 6. Troubleshooting

| Gejala | Penyebab & solusi |
|--------|-------------------|
| Web gagal start, log menyebut login failed | connection string salah / login belum dibuat (bagian 1) |
| `AgentApiKey belum dikonfigurasi` saat ingest | isi `Monitor:AgentApiKey` lalu restart web |
| Agent log 401 | `Agent:ApiKey` tidak sama dengan server |
| Perangkat tetap *Offline* padahal menyala | cek interval agent, firewall outbound 443/5000, dan log agent |
| Grafik kosong di satu panel | data belum masuk untuk rentang tersebut; kirim payload manual atau tunggu siklus berikut |
| Perlu reset data demo | halaman Data & Ingest → *Kosongkan database* |
| Migration gagal di produksi | jalankan `db/migration.sql` manual lalu set `Monitor:AutoMigrate=false` |

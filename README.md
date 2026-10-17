# Device Monitoring Dashboard — Departemen IT

Dashboard monitoring perangkat kantor (PC / laptop / workstation) berbasis **ASP.NET Core 8 (C#) + SQL Server (MSSQL)**.
Data masuk ke database melalui **agent collector** di tiap perangkat (`POST /api/ingest`), lalu divisualisasikan
ke dalam 5 seksi monitoring + halaman detail perangkat + halaman pengelolaan data.

```
agent (tiap PC) ──POST /api/ingest──▶ ASP.NET Core Web ──▶ SQL Server
        ▲                                     │
        │ offline queue (file JSON)           ├── 01 Executive Overview
        └── retry otomatis                    ├── 02 Hardware & System Inventory
                                              ├── 03 Storage & Disk Health
                                              ├── 04 Agent Health & Telemetry
                                              ├── 05 Software Inventory & Compliance
                                              ├── Detail perangkat
                                              └── Data & Ingest (CSV import, aturan compliance, docs API)
```

## Seksi dashboard

| # | Halaman | Panel |
|---|---------|-------|
| 01 | **Executive Overview** | Total PCs, Online, Offline, Total Fleet Storage (4 KPI) · Fleet Uptime Categories · tren uptime & ketersediaan harian · komposisi departemen & tipe · daftar "perlu perhatian" · tabel status terkini seluruh perangkat |
| 02 | **Hardware & System** | Distribusi model perangkat · variasi & distribusi kapasitas RAM · rata-rata RAM terpakai / Average Fleet RAM · kandidat upgrade RAM · Fleet OS Distribution · Windows version summary (dengan penanda end-of-support) · BIOS versions · tabel inventaris lengkap |
| 03 | **Storage & Disk Health** | Total Fleet Storage · komposisi terpakai/kosong · tren storage 14 hari · monitoring storage per perangkat · partisi C · partisi D & lainnya · **Top 10 PCs with Lowest Storage** · kesehatan disk (Healthy/Warning/Critical) |
| 04 | **Agent Health** | Avg Execution Duration (+P95/max) · tren & sebaran durasi · Agent Sync Status Distribution · hasil run (Success/Partial/Queued/Failed) · Offline Queue Accumulation · Agent Resource Footprint (CPU & memori) · log riwayat aktivitas agent (paged + filter) · error terbaru |
| 05 | **Software & Compliance** | Installed Software Inventory grouped by version (coverage per versi) · fragmentasi versi · publisher · compliance summary (Compliant/Outdated/Prohibited/Unmanaged) · temuan compliance · tabel detail: nama komputer, software, versi, publisher, tanggal instal, ukuran |

Fitur lintas halaman: tema **dark/light** (tersimpan di browser), auto-refresh 60 detik, cetak/PDF,
pencarian & sortir tabel klik-kolom, pagination server-side, halaman detail per perangkat
(tren CPU/RAM/storage/uptime + partisi + software + log agent).

## Struktur proyek

```
DeviceMonitor/
├─ DeviceMonitor.sln
├─ src/
│  ├─ DeviceMonitor.Shared/        # entitas EF + kontrak payload API (dipakai web & agent)
│  └─ DeviceMonitor.Web/           # ASP.NET Core MVC + API + EF Core (SqlServer/Sqlite)
│     ├─ Controllers/              # halaman dashboard
│     ├─ Controllers/Api/          # POST /api/ingest, GET /api/*, filter X-Agent-Key
│     ├─ Data/                     # MonitorDbContext + migration
│     ├─ Services/                 # query dashboard, ingest, import CSV, compliance, seeder demo
│     ├─ Views/                    # Razor (layout, 5 halaman, detail, data)
│     └─ wwwroot/                  # css/site.css + js/charts.js (chart canvas mandiri, tanpa CDN)
├─ agent/
│  └─ DeviceMonitor.Agent/         # collector Windows (WMI + registry), offline queue, bisa jadi Windows Service
├─ db/
│  ├─ migration.sql                # skrip skema SQL Server idempotent (hasil `dotnet ef migrations script`)
│  └─ docker-compose.yml           # SQL Server 2022 untuk development
├─ docs/
│  ├─ DEPLOYMENT.md                # panduan deploy produksi (IIS/Kestrel, service, agent, GPO)
│  └─ preview/                     # screenshot seluruh halaman (dark & light)
└─ tools/
   ├─ install-agent.ps1            # install agent sebagai Windows Service / scheduled task
   ├─ smoke_test.py                # uji otomatis halaman + API ingest + import CSV
   └─ screenshot.py                # ambil screenshot untuk verifikasi visual
```

## Menjalankan cepat (development)

Prasyarat: **.NET SDK 8** dan **SQL Server 2019+** (atau Docker).

```bash
# 1) SQL Server via Docker (opsional bila sudah punya instance)
docker compose -f db/docker-compose.yml up -d

# 2) Sesuaikan connection string & API key
#    src/DeviceMonitor.Web/appsettings.json  ->  ConnectionStrings:SqlServerDb, Monitor:AgentApiKey

# 3) Jalankan web (migration + seed demo otomatis saat database masih kosong)
dotnet run --project src/DeviceMonitor.Web
#    buka http://localhost:5000  (atau port pada launchSettings)
```

Saat database kosong, seeder membuat ±48 perangkat demo + riwayat 14 hari agar semua panel
langsung bisa divalidasi. Matikan lewat `Monitor:Seed:OnStartup = false`.

### Mode demo tanpa SQL Server (SQLite)

Untuk mencoba tanpa SQL Server (mis. di laptop/CI):

```bash
Monitor__Provider=Sqlite Monitor__AgentApiKey=DEMO-KEY dotnet run --project src/DeviceMonitor.Web
```

> Catatan: migration EF di proyek ini ditujukan untuk SQL Server. Pada provider SQLite,
> skema dibuat otomatis dengan `EnsureCreated()` (lihat `Program.cs`).

## Alur data

1. **Agent** (`DeviceMonitor.Agent`) berjalan sebagai Windows Service / scheduled task tiap 6 jam:
   mengumpulkan WMI (`Win32_ComputerSystem`, `Win32_OperatingSystem`, `Win32_BIOS`, `Win32_Processor`,
   `Win32_LogicalDisk`) + registry Uninstall + telemetry (CPU, RAM, jumlah proses, footprint agent).
2. Payload JSON dikirim ke `POST /api/ingest` dengan header `X-Agent-Key`.
3. Server melakukan **upsert**: master `Devices`, latest-state `DiskDrives` & `SoftwareInstalls`
   (deteksi uninstall), append `DeviceSnapshots` (time-series) dan `AgentRuns` (log).
4. Bila server tidak terjangkau, agent menyimpan payload ke antrian offline
   (`%ProgramData%\DeviceMonitor\queue`) dan mengirim ulang pada siklus berikutnya —
   panel *Offline Queue Accumulation* menampilkan akumulasi ini.
5. Status compliance software dihitung dari tabel `ComplianceRules`
   (versi minimum, versi dilarang dengan pola wildcard `8.0.*`, software wajib).

### Import CSV (migrasi dari tools lain)

Halaman **Data & Ingest** menerima CSV dengan header fleksibel (case-insensitive, spasi/underscore
diabaikan): `devices`, `disks`, `software`, `snapshots`, `agentruns`. Template bisa diunduh dari
halaman yang sama. Urutan disarankan: devices → disks → software → snapshots/agentruns.

### API ringkas

| Method | Endpoint | Keterangan |
|--------|----------|------------|
| POST | `/api/ingest` | kirim payload perangkat (wajib `X-Agent-Key`) |
| GET | `/api/ping` | health check agent |
| GET | `/api/summary` | KPI ringkas (untuk widget/Grafana/Power BI) |
| GET | `/api/devices` | daftar perangkat + kondisi terkini |
| GET | `/api/devices/{hostname}/disks` | partisi terkini |
| GET | `/api/devices/{hostname}/software` | inventaris software |

Contoh payload lengkap & skrip PowerShell collector tersedia di halaman **Data & Ingest**
(tab API) dan di `docs/DEPLOYMENT.md`.

## Konfigurasi penting (`appsettings.json`)

| Key | Default | Fungsi |
|-----|---------|--------|
| `ConnectionStrings:SqlServerDb` | localhost,1433 | koneksi MSSQL produksi |
| `Monitor:Provider` | `SqlServer` | ganti `Sqlite` untuk mode demo |
| `Monitor:AgentApiKey` | *(wajib diganti)* | kunci header `X-Agent-Key` |
| `Monitor:AutoMigrate` | `true` | jalankan migration EF saat start |
| `Monitor:OnlineThresholdMinutes` | 30 | batas perangkat dianggap offline |
| `Monitor:DiskWarningPercent` / `DiskCriticalPercent` | 80 / 90 | ambang kesehatan disk |
| `Monitor:RamPressurePercent` | 85 | ambang RAM terpakai "berat" |
| `Monitor:TrendDays` | 14 | rentang grafik tren |
| `Monitor:Seed:OnStartup` | `true` | seed data demo bila DB kosong |

## Keamanan

- Endpoint ingest dilindungi API key (`X-Agent-Key`); server menolak bila key masih nilai bawaan.
- Halaman dashboard saat ini tanpa login — pasang di jaringan internal atau tambahkan
  autentikasi (Windows Auth / Entra ID) sesuai kebijakan. Lihat `docs/DEPLOYMENT.md`.
- Gunakan HTTPS di produksi (reverse proxy IIS/Nginx/Caddy).

## Lisensi komponen

Tanpa dependensi frontend eksternal: grafik digambar dengan canvas murni (`wwwroot/js/charts.js`),
ikon SVG inline, font sistem. Backend: EF Core 8, CsvHelper.

#!/usr/bin/env python3
"""Smoke test seluruh halaman dashboard + API ingest (dijalankan di sandbox)."""
import json
import re
import sys
import urllib.error
import urllib.parse
import urllib.request

BASE = "http://127.0.0.1:5080"
API_KEY = "DEMO-KEY-12345"

PAGES = [
    "/Overview", "/Hardware", "/Storage", "/Agent", "/Software", "/Data",
    "/api/ping", "/api/summary", "/api/devices", "/Data/Template?kind=devices",
]

failures = []


def get(path):
    try:
        with urllib.request.urlopen(BASE + path, timeout=60) as r:
            return r.status, r.read()
    except urllib.error.HTTPError as e:
        return e.code, e.read()
    except Exception as e:  # noqa
        return 0, str(e).encode()


def post(path, data, headers=None):
    req = urllib.request.Request(BASE + path, data=data, method="POST",
                                headers=headers or {})
    try:
        with urllib.request.urlopen(req, timeout=60) as r:
            return r.status, r.read()
    except urllib.error.HTTPError as e:
        return e.code, e.read()
    except Exception as e:  # noqa
        return 0, str(e).encode()


print("== GET pages ==")
for p in PAGES:
    code, body = get(p)
    text = body.decode("utf-8", "ignore")
    ok = code == 200
    marker = "OK " if ok else "FAIL"
    extra = ""
    if p.startswith("/api/summary"):
        extra = " " + text[:180]
    if not ok:
        failures.append((p, code, text[:600]))
    print(f"{marker} {code} {p} ({len(text)} bytes){extra}")

# halaman detail perangkat pertama
code, body = get("/api/devices")
try:
    devices = json.loads(body.decode())
    host = devices[0]["hostname"]
except Exception:
    host = None
if host:
    code, body = get("/Device/Details/" + urllib.parse.quote(host))
    print(f"{'OK ' if code == 200 else 'FAIL'} {code} /Device/Details/{host}")
    if code != 200:
        failures.append(("/Device/Details", code, body.decode('utf-8', 'ignore')[:600]))

print("\n== API ingest ==")
payload = {
    "device": {
        "hostname": "TEST-SMOKE-01", "deviceType": "Laptop", "manufacturer": "Lenovo",
        "model": "ThinkPad T14", "osName": "Microsoft Windows 11 Pro", "osBuild": "22631.3447",
        "osVersion": "10.0.22631", "biosVersion": "N3AET72W", "cpuName": "Intel i7-1355U",
        "cpuCores": 10, "cpuLogicalProcessors": 12, "ramTotalGb": 16,
        "ipAddress": "10.99.1.5", "department": "IT", "location": "HO - Lantai 3",
        "assignedUser": "smoke.test", "agentVersion": "1.0.0", "uptimeSeconds": 90000,
    },
    "disks": [
        {"driveLetter": "C", "label": "System", "mediaType": "SSD", "sizeGb": 476.94, "freeGb": 40.0},
        {"driveLetter": "D", "label": "Data", "mediaType": "HDD", "sizeGb": 931.51, "freeGb": 500.0},
    ],
    "software": [
        {"name": "Google Chrome", "version": "118.0.5993.118", "publisher": "Google LLC", "sizeMb": 300},
        {"name": "DeviceMonitor Agent", "version": "1.0.0", "publisher": "PT Internal IT", "sizeMb": 28},
        {"name": "WinRAR 6.24 (64-bit)", "version": "6.24.0", "publisher": "win.rar GmbH", "sizeMb": 14},
    ],
    "telemetry": {"status": "Online", "cpuUsagePercent": 22.5, "ramUsedGb": 11.4,
                  "uptimeSeconds": 90000, "processCount": 210, "agentCpuPercent": 0.9,
                  "agentMemoryMb": 88.0, "offlineQueueCount": 0},
    "agentRun": {"status": "Success", "durationMs": 2100.4, "agentVersion": "1.0.0",
                 "collectorsRun": "hardware,disks,software,telemetry", "itemsSent": 6,
                 "triggerSource": "smoke-test"},
    "isFullSoftwareInventory": True,
}
code, body = post("/api/ingest", json.dumps(payload).encode(),
                  {"Content-Type": "application/json", "X-Agent-Key": API_KEY})
print(("OK " if code == 200 else "FAIL") + f" {code} POST /api/ingest -> {body.decode()[:300]}")
if code != 200:
    failures.append(("/api/ingest", code, body.decode()[:600]))

# tanpa API key harus ditolak
code, body = post("/api/ingest", json.dumps(payload).encode(), {"Content-Type": "application/json"})
print(("OK " if code == 401 else "FAIL") + f" {code} POST /api/ingest tanpa API key (harus 401)")
if code != 401:
    failures.append(("/api/ingest no-key", code, body.decode()[:300]))

# ingest kedua (update) untuk memastikan upsert tidak duplikat
payload["device"]["ramTotalGb"] = 32
payload["disks"][0]["freeGb"] = 12.0
code, body = post("/api/ingest", json.dumps(payload).encode(),
                  {"Content-Type": "application/json", "X-Agent-Key": API_KEY})
print(("OK " if code == 200 else "FAIL") + f" {code} POST /api/ingest (update) -> {body.decode()[:220]}")
if code != 200:
    failures.append(("/api/ingest update", code, body.decode()[:600]))

code, body = get("/api/devices/TEST-SMOKE-01/disks")
print(("OK " if code == 200 else "FAIL") + f" {code} GET /api/devices/TEST-SMOKE-01/disks -> {body.decode()[:260]}")

code, body = get("/Device/Details/TEST-SMOKE-01")
print(("OK " if code == 200 else "FAIL") + f" {code} GET /Device/Details/TEST-SMOKE-01")

print("\n== Seed data demo (via form POST) ==")


def cookie_jar():
    import http.cookiejar
    cj = http.cookiejar.CookieJar()
    return urllib.request.build_opener(urllib.request.HTTPCookieProcessor(cj))


opener = cookie_jar()


def form_get(path):
    with opener.open(BASE + path, timeout=60) as r:
        return r.status, r.read().decode("utf-8", "ignore")


def form_post(path, fields):
    data = urllib.parse.urlencode(fields).encode()
    req = urllib.request.Request(BASE + path, data=data, method="POST",
                                 headers={"Content-Type": "application/x-www-form-urlencoded"})
    try:
        with opener.open(req, timeout=120) as r:
            return r.status, r.read().decode("utf-8", "ignore")
    except urllib.error.HTTPError as e:
        return e.code, e.read().decode("utf-8", "ignore")


def anti_token(html):
    m = re.search(r'name="__RequestVerificationToken"[^>]*value="([^"]+)"', html)
    return m.group(1) if m else ""


st, html = form_get("/Data")
tok = anti_token(html)
print(f"GET /Data -> {st}, antiforgery token {'ditemukan' if tok else 'TIDAK ADA'}")
if not tok:
    failures.append(("/Data antiforgery", st, html[:400]))

st, html = form_post("/Data/SeedDemo", {"__RequestVerificationToken": tok, "deviceCount": "42", "historyDays": "14"})
ok = st == 200 and "berhasil dibuat" in html
print(("OK " if ok else "FAIL") + f" {st} POST /Data/SeedDemo")
if not ok:
    failures.append(("/Data/SeedDemo", st, html[:800]))

code, body = get("/api/summary")
summary = json.loads(body.decode())
print("   ringkasan setelah seed:", json.dumps(summary, ensure_ascii=False)[:260])
if summary.get("totalDevices", 0) < 40:
    failures.append(("seed count", 200, json.dumps(summary)))

print("\n== Import CSV (multipart) ==")
import uuid


def multipart(fields, files):
    boundary = "----dm" + uuid.uuid4().hex
    lines = []
    for k, v in fields.items():
        lines += [f"--{boundary}", f'Content-Disposition: form-data; name="{k}"', "", v]
    for k, (fn, content) in files.items():
        lines += [f"--{boundary}",
                  f'Content-Disposition: form-data; name="{k}"; filename="{fn}"',
                  "Content-Type: text/csv", "", content]
    lines += [f"--{boundary}--", ""]
    body = "\r\n".join(lines).encode()
    return body, f"multipart/form-data; boundary={boundary}"


st, html = form_get("/Data")
tok = anti_token(html)
csv_devices = ("hostname,devicetype,manufacturer,model,osname,osbuild,ramtotalgb,department,location,assigneduser,status,lastseenutc\n"
               "PC-CSV-01,Desktop,Dell,OptiPlex 7010,Microsoft Windows 11 Pro,22631.3447,16,Finance,HO - Lantai 2,csv.user,Online,2026-10-17T02:00:00Z\n"
               "PC-CSV-02,Laptop,Lenovo,ThinkPad T14,Microsoft Windows 10 Pro,19045.4291,8,IT,HO - Lantai 3,csv.two,Online,2026-10-17T02:05:00Z\n")
body, ctype = multipart({"__RequestVerificationToken": tok, "kind": "devices"},
                        {"file": ("devices.csv", csv_devices)})
req = urllib.request.Request(BASE + "/Data/Import", data=body, method="POST", headers={"Content-Type": ctype})
try:
    with opener.open(req, timeout=120) as r:
        st, html = r.status, r.read().decode("utf-8", "ignore")
except urllib.error.HTTPError as e:
    st, html = e.code, e.read().decode("utf-8", "ignore")
ok = st == 200 and "PC-CSV-01" in html or "2 baris baru" in html or "Import devices.csv" in html
print(("OK " if st == 200 else "FAIL") + f" {st} POST /Data/Import (devices.csv)")
if st != 200:
    failures.append(("/Data/Import devices", st, html[:800]))

code, body = get("/api/devices?hostname=PC-CSV")
print("   perangkat hasil import:", body.decode()[:300])

print("\n== Chart JSON blocks per halaman ==")
for p in ["/Overview", "/Hardware", "/Storage", "/Agent", "/Software"]:
    code, body = get(p)
    text = body.decode("utf-8", "ignore")
    charts = re.findall(r'<script type="application/json" id="([^"]+)"', text)
    kpi = len(re.findall(r'class="kpi"', text))
    rows = len(re.findall(r"<tr>", text))
    empty = text.count("Belum ada data")
    print(f"{p}: {len(charts)} chart, {kpi} KPI, ~{rows} baris tabel, {empty} panel kosong")
    if code != 200 or len(charts) == 0:
        failures.append((p + " charts", code, "no charts"))

print("\n== RESULT ==")
if failures:
    for f in failures:
        print("FAIL:", f[0], f[1])
        print(f[2][:500])
    sys.exit(1)
print("ALL CHECKS PASSED")


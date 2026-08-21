# Panduan Pengukuhan Kiosk Makmal: Kawalan Pelaksanaan AppLocker & SRP
## Lab Kiosk Hardening: AppLocker & Software Restriction Policies (SRP)

**Rujukan Seni Bina:** `Visual_SSO/Technical_Architecture_Visual_SSO.md` §9 & §3.5  
**Rujukan PRD:** `Visual_SSO/PRD_Visual_SSO_v2.md` §8.3  
**Sasaran:** Penyelaras ICT Sekolah / Pentadbir Makmal Komputer

---

## 1. Rasional & Model Ancaman (Threat Model)

Storan kelayakan setempat (`credentials.dat`) dilindungi pada dua lapisan asas:
1. **DPAPI Skop `LocalMachine`:** Memastikan fail storan tidak boleh dinyahsulit sekiranya disalin ke komputer lain.
2. **Kawalan Capaian Fail (ACL):** Menghalang akaun murid biasa daripada membaca atau membuka fail storan melalui penjelajah fail (Explorer).

> [!CAUTION]
> **Had DPAPI Skop Mesin:**  
> DPAPI pada skrip `LocalMachine` membolehkan sebarang kod atau perisian yang berjalan di atas komputer berkenaan memanggil API nyahsulit. Sekiranya seorang murid dapat menjalankan fail binari luaran (contohnya daripada pemacu kilat USB, direktori `%TEMP%`, atau memanggil skrip PowerShell sebarangan), kelayakan sekolah berisiko diekstrak.
> 
> **Kawalan pelaksanaan (Execution Control melalui AppLocker/SRP) adalah kawalan keselamatan paling kritikal (load-bearing control) yang memisahkan sesi makmal murid daripada pendedahan kata laluan.**

---

## 2. Mengapa AppLocker Tidak Dilaksanakan Melalui Kod Pemasang

Pemasang DELIMa Smart Launcher sengaja **TIDAK** menyediakan kotak pilihan automatik untuk memasang AppLocker:
- **Kebergantungan Edisi Windows:** AppLocker memerlukan Windows 10/11 Enterprise atau Education. Pada edisi Windows Home atau Pro tanpa pengurusan GPO, arahan AppLocker akan gagal secara senyap.
- **Risiko Keselamatan Palsu:** Kotak pilihan automatik yang gagal secara senyap akan membuatkan Penyelaras ICT beranggapan bahawa makmal telah selamat, walhal tiada sebarang sekatan berjalan.
- **Kepatuhan:** Penyelaras ICT mesti mengesahkan pelaksanaan dasar secara manual atau melalui GPO domain sekolah, dan menanda ruangan wajib pada **Senarai Semak Makmal (Lab Checklist)**.

---

## 3. Skrip PowerShell AppLocker (Windows Enterprise / Education)

Jalankan skrip PowerShell berikut dalam sesi **Administrator** pada setiap PC makmal (atau edarkan melalui Active Directory Group Policy / Microsoft Intune):

```powershell
<#
.SYNOPSIS
    Mengukuhkan PC Makmal DELIMa dengan dasar AppLocker.
    Menghalang pelaksanaan fail .exe dan skrip daripada pemacu USB dan %TEMP% untuk akaun murid.
#>

# 1. Pastikan Perkhidmatan Application Identity (AppIDSvc) berjalan secara automatik
Write-Host "[1/3] Mengkonfigurasi perkhidmatan Application Identity (AppIDSvc)..." -ForegroundColor Cyan
Set-Service -Name "AppIDSvc" -StartupType Automatic
Start-Service -Name "AppIDSvc"

# 2. Cipta peraturan asas AppLocker:
#    - Benarkan semua fail dalam %ProgramFiles% dan %SystemRoot% (Windows)
#    - Benarkan Pentadbir (Administrators) menjalankan semua perisian
#    - Sekat akaun murid/pengguna biasa daripada menjalankan perisian di luar direktori sistem
Write-Host "[2/3] Menjana dasar peraturan AppLocker..." -ForegroundColor Cyan

$AppLockerXml = @"
<AppLockerPolicy Version="1">
  <RuleCollection Type="Exe" EnforcementMode="Enabled">
    <!-- Peraturan Lalai Pentadbir: Akses Penuh -->
    <FilePathRule Id="921cc481-6e17-4653-b6f5-03214a27a333" Name="Semua fail untuk Pentadbir" Description="Membenarkan ahli kumpulan Administrators menjalankan semua aplikasi." UserOrGroupSid="S-1-5-32-544" Action="Allow">
      <Conditions>
        <FilePathCondition Path="*" />
      </Conditions>
    </FilePathRule>
    <!-- Benarkan program dalam Program Files -->
    <FilePathRule Id="a61c8b2c-a319-4cd0-9690-d2177cad7b51" Name="Semua fail dalam Program Files" Description="Membenarkan aplikasi dipasang dalam Program Files." UserOrGroupSid="S-1-1-0" Action="Allow">
      <Conditions>
        <FilePathCondition Path="%PROGRAMFILES%\*" />
      </Conditions>
    </FilePathRule>
    <!-- Benarkan fail sistem dalam folder Windows -->
    <FilePathRule Id="324ef3e4-104c-477f-88ea-cb16097da358" Name="Semua fail dalam direktori Windows" Description="Membenarkan fail sistem Windows." UserOrGroupSid="S-1-1-0" Action="Allow">
      <Conditions>
        <FilePathCondition Path="%WINDIR%\*" />
      </Conditions>
    </FilePathRule>
  </RuleCollection>
  <RuleCollection Type="Script" EnforcementMode="Enabled">
    <FilePathRule Id="06d3f37a-4ab9-4735-beb0-209fe919a79c" Name="Skrip dalam Program Files" Description="Membenarkan skrip dalam Program Files." UserOrGroupSid="S-1-1-0" Action="Allow">
      <Conditions>
        <FilePathCondition Path="%PROGRAMFILES%\*" />
      </Conditions>
    </FilePathRule>
    <FilePathRule Id="9428c775-a679-4072-aa8c-e950472b30e9" Name="Skrip dalam direktori Windows" Description="Membenarkan skrip sistem Windows." UserOrGroupSid="S-1-1-0" Action="Allow">
      <Conditions>
        <FilePathCondition Path="%WINDIR%\*" />
      </Conditions>
    </FilePathRule>
  </RuleCollection>
</AppLockerPolicy>
"@

$TempPolicyFile = [System.IO.Path]::Combine([System.IO.Path]::GetTempPath(), "DelimaAppLockerPolicy.xml")
$AppLockerXml | Out-File -FilePath $TempPolicyFile -Encoding utf8

# 3. Pasang dan kuatkuasakan dasar AppLocker
Write-Host "[3/3] Memuatkan dan menguatkuasakan dasar AppLocker..." -ForegroundColor Cyan
Set-AppLockerPolicy -XmlPolicy $TempPolicyFile

Remove-Item -Path $TempPolicyFile -Force
Write-Host "[BERJAYA] Pengukuhan AppLocker selesai. Murid tidak dapat menjalankan binari dari pemacu USB atau %TEMP%." -ForegroundColor Green
```

---

## 4. Panduan untuk Windows 10/11 Pro (Software Restriction Policies / SRP)

Sekiranya makmal sekolah menggunakan Windows 10/11 Pro yang tidak menyokong modul PowerShell `Set-AppLockerPolicy`:
1. Buka `secpol.msc` (Local Security Policy).
2. Pergi ke **Software Restriction Policies** -> Klik kanan dan pilih **New Software Restriction Policies**.
3. Tetapkan **Security Levels** default kepada `Disallowed`.
4. Di bawah **Additional Rules**, pastikan laluan berikut dibenarkan (`Unrestricted`):
   - `%HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\ProgramFilesDir%`
   - `%HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion\SystemRoot%`
5. Sahkan bahawa peraturan terpakai kepada *All users except local administrators*.

---

## 5. Ruangan Wajib Senarai Semak Makmal (Lab Checklist Requirement)

Per `PRD_Visual_SSO_v2.md` §8.3, setiap PC makmal yang disediakan mesti melalui pengesahan berikut sebelum dibuka kepada murid:

| Komponen | Status Wajib | Tindakan Pengesahan Penyelaras ICT |
| :--- | :--- | :--- |
| **Penyediaan Storan (Provisioning)** | `[X] Selesai` | `credentials.dat` wujud dengan saiz > 0 bait |
| **Had Capaian Fail (Store ACL)** | `[X] Disahkan` | Buka `%ProgramData%\DELIMa Launcher\credentials.dat` menggunakan akaun murid -> **Mesti Access Denied** |
| **Kawalan AppLocker / SRP** | `[ ] Wajib Disahkan` | Cuba jalankan sebarang fail `.exe` atau skrip `.ps1` dari pemacu USB pada akaun murid -> **Mesti disekat oleh Windows** |
| **Dasar Chrome Perusahaan** | `[X] Opt-In` | Buka Chrome -> `chrome://settings/passwords` tidak membenarkan simpanan kata laluan, F12 disekat |

### Baris Pengesahan Senarai Semak:
```text
[ ] Dasar Kawalan Pelaksanaan AppLocker / SRP telah dikuatkuasakan untuk akaun murid pada komputer ini: [ ] DISAHKAN (Tarikh: ___________, Oleh: ___________)
```

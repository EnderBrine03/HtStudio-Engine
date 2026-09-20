# HtStudio Engine (C# / Windows)

**HtStudio Launcher** = bağımsız masaüstü uygulaması (Rockstar Games Launcher benzeri):
kütüphane listesi, uygulama ekle, HTSTUDIO_APP_V1 doğrula, Oynat.


Plana uygun **Windows-native** launcher:

- **HtStudioLauncher** — `.hts` paketini açar, etiket/türü okur, hash doğrular, **HTML** içeriği **WebView2** ile gösterir
- **HtStudioPack** — proje klasöründen `.hts` üretir
- **htpack yok** — format tamamen `.hts`
- Token = **format etiketi** (login/şifre değil): magic `HTS1` + tür + manifest

## Gereksinimler

- Windows 10/11
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (derleme için)
- [WebView2 Runtime](https://developer.microsoft.com/microsoft-edge/webview2/) (çoğu Win10/11'de yüklü)

## Derleme

```bat
dotnet build HtStudioEngine.sln -c Release
```

Çıktı:
- `src\HtStudioLauncher\bin\Release\net8.0-windows\HtStudioLauncher.exe`
- `src\HtStudioPack\bin\Release\net8.0\HtStudioPack.exe`

## Paket üret

```bat
dotnet run --project src\HtStudioPack -c Release -- samples\hello-html hello.hts html
```

## Çalıştır

```bat
HtStudioLauncher.exe hello.hts
```

veya launcher'ı açıp `.hts` seç.

## Paket formatı (.hts)

```
[4] magic "HTS1"
[1] format version = 1
[1] type (1=html, 2=python, 3=java)
[4] manifest length (LE)
[N] manifest JSON (id, name, version, entry, type, contentHash)
[4] payload length (LE)
[N] payload = ZIP(bytes) XOR FormatKey
```

Python/Java türleri etikette reserved; bu sürüm yalnızca HTML çalıştırır.

## Mimari not

- Sahte **exe imzası** → Windows Authenticode
- **HtStudio paketi mi / türü ne / bozulmuş mu** → Launcher (`HTS1` + hash)


## HtStudio authenticity (Hugging Face builder)

Space: [CrimsonSorcerer/htstudio-builder-exxe](https://huggingface.co/spaces/CrimsonSorcerer/htstudio-builder-exxe)

**Magic:** `HTSTUDIO_APP_V1`

Launcher checks (in order):

1. `htstudio.marker` or `*.htstudio` next to the `.exe` (JSON must contain `"magic":"HTSTUDIO_APP_V1"`)
2. `resources/htstudio.marker` (Electron install layout)
3. Binary scan for ASCII `HTSTUDIO_APP_V1` inside the `.exe`

If none match:

> Sorry, this application was not developed with HtStudio. HtStudio Launcher only supports applications developed with HtStudio.

Valid EXE → Launcher starts it with `Process.Start`.

`.hts` packages (native HTML via WebView2) still work as before.

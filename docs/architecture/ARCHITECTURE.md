| **Bunker.Config** | var | yok | `config/schema` ve `config/balance` dosyalarindan URETILEN ScriptableObject siniflari. Elle kod yazilmaz; herkes bagimli olabilir, kendisi yalnizca `Bunker.Systems`e bagimlidir (ADR-0005) |
| Bunker.Gameplay | var | (eklenecek) |# Mimari

**Sahibi:** unity-architect · **Son güncelleme:** 2026-08-29
**Kod adı:** Bunker · **Durum:** Unity 6000.3.23f1 bağlandı, derleme temiz

---

## 1. Temel kural

> **Oyun mantığı Unity'den bağımsız, saf C# olarak yazılır.**

Bu bir stil tercihi değil, bu projenin çalışma biçiminin ön şartı. Mantık
MonoBehaviour'lara gömülürse:

- Her denge değişikliği Unity açıp oynamayı gerektirir
- Kart etkileşimleri test edilemez, sadece denenir
- Netcode kütüphanesi değişirse (ADR-0004, geri dönüşü *high*) mantık da gider

Ve en önemlisi: `Bunker.Systems` derleyici tarafından Unity'ye kapatıldığı için bu kural
**dilek değil, hata mesajı.**

---

## 2. Assembly haritası

```
Assets/_Project/Code/
  Systems/   Bunker.Systems         saf C#, noEngineReferences: true
  Config/    Bunker.Config          uretilen ScriptableObject ayar siniflari
  Gameplay/  Bunker.Gameplay        MonoBehaviour, Systems'i cagirir
  Net/       Bunker.Net             Mirror, Systems'i cagirir
  AI/        Bunker.AI              NavMesh, zombi davranisi
  UI/        Bunker.UI              HUD, draft ekrani, sicil
  Editor/    Bunker.Editor          sadece editor araclari
  Tests/     Bunker.Systems.Tests   Systems icin EditMode testleri
```

| Assembly | Unity'ye erişimi | Mirror'a erişimi | Neyi barındırır |
|---|---|---|---|
| **Bunker.Systems** | **yok** | **yok** | Tur ölçekleme, ekonomi, kart havuzu ve draft mantığı, hasar hesabı (SYS-02 §3), drop tablosu, silah alışkanlık eşikleri, sicil metrikleri |
| **Bunker.Config** | var | yok | `config/schema` ve `config/balance` dosyalarından **üretilen** ScriptableObject ayar sınıfları. Elle kod yazılmaz. Herkes ona bağımlı olabilir; kendisi yalnızca `Bunker.Systems`'e bağımlıdır (ADR-0005) |
| Bunker.Gameplay | var | (eklenecek) | Oyuncu, silah davranışı, barikat, kapı, tuzak |
| Bunker.Net | var | (eklenecek) | Host otoritesi, zombi toplu snapshot, lag compensation, RPC yüzeyi |
| Bunker.AI | var | yok | NavMesh agent'ları, zombi durum makinesi |
| Bunker.UI | var | yok | HUD, draft ekranı, run sonu sicil |
| Bunker.Editor | var (Editor) | — | Kart dengeleme penceresi, harita istatistiği |
| Bunker.Systems.Tests | Editor | yok | `Bunker.Systems` birim testleri |

**Bağımlılık yönü tek yönlüdür:** dıştaki katmanlar `Systems`'i çağırır, `Systems` hiçbir
şeyi çağırmaz. Ok işareti asla ters dönmez.

---

## 3. Derleyicinin zorladığı sınırlar

| Kural | Nasıl zorlanıyor |
|---|---|
| Oyun mantığı Unity'ye bağlanamaz | `Bunker.Systems.asmdef` → `noEngineReferences: true` |
| Oyun mantığı Mirror'a bağlanamaz | `Bunker.Systems` referans listesi boş (ADR-0004) |
| AI ve UI birbirini çağıramaz | Karşılıklı referans yok |
| Editör kodu build'e sızamaz | `Bunker.Editor` → `includePlatforms: ["Editor"]` |

Bir referans eklemek bu tablodan bir satır silmek demektir. Referans ekleyen commit,
gerekçesini `docs/DECISIONS.md`'ye yazar.

---

## 4. Ayar verisi nerede yaşar

```
config/balance/*.json     <- SSoT. Her denge sayisi burada.
config/schema/*.json      <- her balance dosyasinin semasi
        |
        v  (uretim adimi)
Assets/_Project/Config/   <- uretilen ScriptableObject'ler. ELLE DUZENLENMEZ.
```

C# içine gömülmüş bir denge sayısı bir hatadır (CLAUDE.md, madde 5). Sebep pratik:
JSON'u Unity açmadan okuyabilir, diff'leyebilir ve test edebilirim; `.asset` YAML'ını
okuyamam — hook zaten engelliyor.

---

## 5. Durum

**Tamamlanan (2026-08-29):**

- Unity 6000.3.23f1 projesi bağlandı; headless import ve derleme temiz (`return code 0`)
- URP 17.3.0 · AI Navigation 2.0.14 · Test Framework 1.6.0 · Input System 1.20.0
- Yedi assembly tanımı Unity tarafından tanındı
- URP pipeline asset'leri `Assets/_Project/Settings/` altına taşındı; `GraphicsSettings`
  ve `QualitySettings` referansları GUID üzerinden korundu
- `unityyamlmerge` sürücüsü git'e kaydedildi

**Bekleyen:**

- Mirror kurulmadı; `Bunker.Net` ve `Bunker.Gameplay` referansları kurulumdan sonra eklenecek
- Zombi toplu snapshot tasarımı — M0-07 çıktısı
- `config/` şemaları — ilk denge sayıları çıkınca
- `Bunker.Systems` içinde henüz kod yok (boş assembly uyarısı beklenen durumdur)

## 6. Klasör düzeni

```
Assets/_Project/
  Code/     yukaridaki assembly'ler
  Config/   uretilen ScriptableObject'ler (elle duzenlenmez)
  Data/     calisma zamani JSON aynasi
  Art/      Concept Generated Textures Sprites Materials Models VFX UI
  Audio/    Music SFX Mixers
  Prefabs/  Gameplay UI VFX Net
  Scenes/   Boot Menu Levels Sandbox
  Settings/ pipeline ve kalite ayarlari
```

Üçüncü parti paketler `Assets/` kökünde kendi klasörlerinde durur ve **düzenlenmez**.

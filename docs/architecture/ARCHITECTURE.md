# Mimari

**Sahibi:** unity-architect · **Son güncelleme:** 2026-08-29
**Kod adı:** Bunker · **Durum:** iskelet kuruldu, Unity projesi henüz bağlanmadı

---

## 1. Temel kural

> **Oyun mantığı Unity'den bağımsız, saf C# olarak yazılır.**

Bu bir stil tercihi değil, bu projenin çalışma biçiminin ön şartı. Mantık
MonoBehaviour'lara gömülürse:

- Her denge değişikliği Unity açıp oynamayı gerektirir
- Kart etkileşimleri test edilemez, sadece denenir
- Netcode kütüphanesi değişirse (ADR-0001, geri dönüşü *high*) mantık da gider

Ve en önemlisi: `Bunker.Systems` derleyici tarafından Unity'ye kapatıldığı için bu kural
**dilek değil, hata mesajı.**

---

## 2. Assembly haritası

```
Assets/_Project/Code/
  Systems/   Bunker.Systems         saf C#, noEngineReferences: true
  Gameplay/  Bunker.Gameplay        MonoBehaviour, Systems'i cagirir
  Net/       Bunker.Net             FishNet, Systems'i cagirir
  AI/        Bunker.AI              NavMesh, zombi davranisi
  UI/        Bunker.UI              HUD, draft ekrani, sicil
  Editor/    Bunker.Editor          sadece editor araclari
  Tests/     Bunker.Systems.Tests   Systems icin EditMode testleri
```

| Assembly | Unity'ye erişimi | FishNet'e erişimi | Neyi barındırır |
|---|---|---|---|
| **Bunker.Systems** | **yok** | **yok** | Tur ölçekleme, ekonomi, kart havuzu ve draft mantığı, hasar hesabı (SYS-02 §3), drop tablosu, silah alışkanlık eşikleri, sicil metrikleri |
| Bunker.Gameplay | var | (eklenecek) | Oyuncu, silah davranışı, barikat, kapı, tuzak |
| Bunker.Net | var | (eklenecek) | Host otoritesi, zombi toplu snapshot, RPC yüzeyi |
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
| Oyun mantığı FishNet'e bağlanamaz | `Bunker.Systems` referans listesi boş (ADR-0001) |
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

## 5. Henüz yapılmamış olanlar

- Unity projesi bağlanmadı — `Assets/`, `Packages/`, `ProjectSettings/` bekleniyor
- FishNet kurulmadı; `Bunker.Net` ve `Bunker.Gameplay` referansları kurulumdan sonra eklenecek
- `unityyamlmerge` sürücüsü git'e kaydedilmedi (Unity yolu gerekiyor)
- Zombi toplu snapshot tasarımı — M0 çıktısı
- `config/` şemaları — ilk denge sayıları çıkınca

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

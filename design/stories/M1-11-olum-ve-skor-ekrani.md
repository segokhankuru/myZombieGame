# M1-11 — Ölüm, run sonu, skor ekranı

| Alan | Değer |
|---|---|
| **Durum** | In review |
| **Milestone** | M-01 |
| **Tip** | UI + Integration |
| **Sahip** | `gameplay-programmer` (kod) → `ui-programmer` (ekran) |
| **Assembly** | `Bunker.Systems`, `Bunker.Gameplay`, `Bunker.AI`, `Bunker.UI` |
| **Tahmin** | 2 gün |
| **Bağımlılık** | M1-01 ✅, M1-02 ✅, M1-04 🔧, M1-05 🔧, M1-06 🔧 — hepsi kod olarak bitti |
| **Sistem** | SYS-01 (ödül ve tanınma) |
| **Pillar** | PILLAR-02 (kazanılan her puan görünür), PILLAR-03 (kesintisiz tur) |

---

## Tasarım niyeti

Run'ın bir **sonu** yok. `DebugPlayerHealth` canı sessizce dolduruyor; oyuncu ölmüyor,
dolayısıyla hiçbir tur bir şey ifade etmiyor. Skorun anlamı kaybedilebilir olmasından
gelir — kaybedilemeyen bir skor sayaçtır, skor değil.

Bu iş üç şeyi getirir:

1. **Gerçek oyuncu canı.** Vurulunca düşer, vurulmayınca yenilenir.
2. **Run sonu.** Can sıfırlanınca run biter; zombiler durur, girdi kesilir.
3. **Skor ekranı.** "Ne kadar iyiydim" sorusunun cevabı, tek ekranda.

**FEELS LIKE:** *"Az kalsın ölüyordum" ile "öldüm" arasındaki fark okunabilmeli. Ölüm
sürpriz olmamalı — canın azaldığını hasar aldığın anda görmüş olmalısın. Skor ekranı
suçlamamalı; bir sonraki denemeyi başlatmalı.*

**Can modeli kararı (geliştirici, 2026-09-03):** vurulmayınca yenilenir. Gerekçesi
ÇK-17: kalıcı hasar birikimi 20 dakikalık bir oturumda ceza hissi yaratır ve tekrar
oynama isteğini düşürür. Kaçmak ve köşe tutmak taktik kalmalı.

**Yeniden başlatma kararı:** skor ekranında **R** yeni run başlatır. Play'den çıkıp
tekrar basmak, her denemede bir test döngüsü kaybı demek.

---

## Kabul kriterleri

**AC-1 — Can düşer ve yenilenir**
> **Given** oyuncu tam canlı
> **When** bir zombi vurur
> **Then** can `zombie.json`'daki hasar kadar düşer, `regenDelaySeconds` boyunca sabit
> kalır, sonra `regenPerSecond` hızıyla tavana kadar dolar.

**AC-2 — Yeni hasar yenilenmeyi sıfırlar**
> **Given** can yenilenmeye başlamış
> **When** oyuncu tekrar vurulur
> **Then** yenilenme durur ve gecikme sayacı baştan başlar.

**AC-3 — Can sıfırlanınca run biter**
> **Given** oyuncu hayatta
> **When** can sıfıra iner
> **Then** run **bir kez** biter (aynı karede ikinci hasar ikinci run sonu üretmez),
> zombi doğumu durur, oyuncunun girdisi kesilir.

**AC-4 — Skor ekranı doğru sayıları gösterir**
> **Given** run bitti
> **When** skor ekranı gelir
> **Then** ulaşılan tur, run süresi, öldürme sayısı, kafa vuruşu sayısı ve kazanılan
> toplam puan, run boyunca gerçekten olanla birebir aynıdır.

**AC-5 — R yeni bir run başlatır**
> **Given** skor ekranı açık
> **When** R'ye basılır
> **Then** tur 1'in molasından temiz başlanır: sahada zombi yok, can tam, puan sıfır,
> satın alınmış kapılar tekrar kilitli, sayaçlar sıfır.

**AC-6 — İkinci run'ın sayıları birincisinden sızmaz**
> **Given** bir run bitti ve yenisi başlatıldı
> **When** ikinci run da biter
> **Then** skor ekranı yalnızca ikinci run'ın sayılarını gösterir.

**AC-7 — Ölüm sürpriz değil**
> **Given** can `lowHealthFraction` eşiğinin altında
> **When** oyuncu ekrana bakar
> **Then** ekran kenarında bir uyarı vardır ve can göstergesi HUD'da okunur.

---

## Mimari yönlendirme

**Sorun:** skor ekranı `Bunker.UI`'de yaşar; UI yalnızca `Bunker.Systems` ve
`Bunker.Gameplay`'i görür. Turu yürüten `ZombieDirector` ise `Bunker.AI`'da ve UI onu
**göremez**. Aynı sorun M1-08'de `RoundSignals` ile çözüldü: iki tarafın da gördüğü tek
yer `Bunker.Systems`.

**Karar:** aynı desen tekrarlanır. `Bunker.Systems.Rounds` altına:

| Tip | Ne | Nerede |
|---|---|---|
| `RunRecorder` | Saf C#. Run'ın sayaçları: süre, tur, öldürme, kafa vuruşu, puan. | `Systems/Rounds/RunRecorder.cs` |
| `RunSummary` | Dondurulmuş sonuç. Skor ekranının okuduğu tek veri. | aynı dosya |
| `RunSignals` | `RoundSignals`'ın kardeşi: `RunEnded`, `RunRestarted`, canlı `RunRecorder`. | `Systems/Rounds/RunSignals.cs` |

`RoundSignals`'ın statik olmanın bedeli için yazdığı iki kural **aynen geçerlidir**: her
abone `OnDisable`'da bırakır, `Clear()` yalnızca açılışta `RunSignalsBootstrap`'ten
çağrılır.

**Veri akışı — kimse kimseye uzanmaz:**

```
ZombieDirector (AI) ──► RunSignals.Current.Tick / NoteRound
PlayerScore (Gameplay) ─► RunSignals.Current.NoteKill / NoteScore
PlayerHealth (Gameplay) ─► RunSignals.RaisePlayerDied()
                                    │
                    RunEnded ───────┼──────► GameOverHud (UI)   gösterir
                                    ├──────► ZombieDirector      durur
                                    └──────► PlayerController    girdiyi keser
GameOverHud ──► RunSignals.RequestRestart()
                    RunRestarted ──► direktör / can / puan / kapı  sıfırlanır
```

**Otorite (ADR-0004):** can ve run sonu kalıcı sonucu olan şeylerdir → sunucu yazar.
`PlayerHealth` bir `NetworkBehaviour`'dur, can `[SyncVar]`'dır. M-01 solo host modunda
koşar ama seam yerinde durur.

**`DebugPlayerHealth` silinir.** Kendi TODO'su bu işi işaret ediyordu.

---

## Config anahtarları

**Yeni alan: `player`.** Şu an yok — **şema → importer → tüketici sırası zorunlu**
(handoff, tuzak 1). Kod üretilmeden `PlayerConfigAsset`'e bağlanan bir dosya projeyi
derlenemez yapar ve importer da o noktadan sonra koşamaz.

`config/schema/player.schema.json` + `config/balance/player.json`:

| Anahtar | Değer | Aralık | Neden bu |
|---|---|---|---|
| `health.maxPoints` | 100 | 50–500 | `zombie.json`'daki `damage: 30` açıklaması "oyuncu canı 100 kabul edilir" diyor. Ayrılırsa o açıklama anlamsızlaşır. 100 can = 4 vuruş. |
| `health.regenDelaySeconds` | 4.0 | 1–15 | Vuruş sonrası yenilenmenin başlaması. Kısaltırsan hasar bir sonuç olmaktan çıkar; uzatırsan bir kötü köşe bütün turu cezalandırır. |
| `health.regenPerSecond` | 25 | 5–200 | Sıfırdan tam cana 4 saniye. Hızlandırırsan can bir kaynak olmaktan çıkar; yavaşlatırsan kaçmak işe yaramaz ve oyun bekleme oyununa döner. |
| `health.lowFraction` | 0.35 | 0.1–0.6 | Uyarının göründüğü eşik. Bir vuruşluk canın (0.30) hemen üstünde: uyarıyı gördüğünde bir vuruş hakkın kalmış olmalı. |

---

## Dokunulacak dosyalar

**Yeni**
- `config/schema/player.schema.json`
- `config/balance/player.json`
- `Assets/_Project/Code/Systems/Rounds/RunRecorder.cs` (`Bunker.Systems`)
- `Assets/_Project/Code/Systems/Rounds/RunSignals.cs` (`Bunker.Systems`)
- `Assets/_Project/Code/Gameplay/PlayerHealth.cs` (`Bunker.Gameplay`)
- `Assets/_Project/Code/UI/GameOverHud.cs` (`Bunker.UI`)
- `Assets/_Project/Code/Tests/RunRecorderTests.cs`
- `Assets/_Project/Code/Tests/RegeneratingHealthTests.cs` + `RunSignalsTests.cs`

**Değişen**
- `Assets/_Project/Code/AI/ZombieDirector.cs` — run bitince dur, restart'ta sıfırla, `NoteRound`
- `Assets/_Project/Code/Gameplay/PlayerScore.cs` — `NoteKill`, `NoteScore`, restart'ta sıfırla
- `Assets/_Project/Code/Gameplay/PlayerController.cs` — run bitince girdi kes
- `Assets/_Project/Code/Gameplay/PurchasableDoor.cs` — restart'ta tekrar kilitle
- `Assets/_Project/Code/Gameplay/RoundSignalsBootstrap.cs` — `RunSignals.Clear()` ekle
- `Assets/_Project/Code/UI/CombatHud.cs` — can göstergesi + düşük can uyarısı
- `Assets/_Project/Code/Editor/ZombieSetup.cs` — `PlayerHealth` + `GameOverHud` bağla

**Silinen**
- `Assets/_Project/Code/AI/DebugPlayerHealth.cs` (+ `.meta`)

---

## Kapsam dışı

- Düşme / kendini toparlama / diriltme — multiplayer'ın işi, M-02
- Telemetrinin diske yazılması — **M1-12**. Bu iş sayaçları üretir, kaydetmez.
- En yüksek skor tablosu, kalıcılık, kayıt dosyası
- Ölüm animasyonu, kamera efekti, ses — sanat aşaması
- Yerelleştirme anahtarları — gri kutu HUD IMGUI, M-01'de metin gömülü (CombatHud ile aynı gerekçe)
- Ana menü

---

## Test senaryoları

`RunRecorder` (EditMode, saf C#, sahnesiz — ÇK-16):
- AC-4: tur/öldürme/kafa vuruşu/puan sayaçları gerçekleşene göre ilerler
- AC-4: `Freeze()` sonrası gelen bildirimler özeti değiştirmez
- AC-6: `Reset()` her sayacı sıfırlar; ikinci run birincinin sayısını taşımaz
- Kenar: negatif delta, sıfır öldürme, aynı karede iki ölüm bildirimi

`PlayerHealthLogic` (EditMode, `HealthPool` üstünde saf mantık):
- AC-1: hasar → gecikme boyunca sabit → yenilenme
- AC-1: yenilenme tavanı aşmaz
- AC-2: yenilenme sırasında hasar gecikmeyi sıfırlar
- AC-3: sıfır can **bir kez** run sonu üretir; ikinci hasar ikincisini üretmez
- Kenar: sıfır hasar, tam candan tek vuruşta ölüm, ölü haldeyken hasar

---

## Gereken kanıt

| Kanıt | Nasıl |
|---|---|
| EditMode testleri yeşil | `.claude/tools/unity-test.ps1` |
| Derleme temiz | `.claude/tools/unity-log.ps1 -Errors` |
| Config doğrulaması | `.claude/tools/config-validate.ps1` |
| **Oyun testi** | Play → öl → skor ekranı doğru mu → R → temiz ikinci run |

Son satır pazarlık konusu değil: bu oturumdaki beş hatanın beşi de oyun testinden çıktı,
hiçbiri testlerden çıkmadı.

---

## Sonuç (2026-09-03)

**Durum:** In review — kod bitti, **oyun testi bekliyor**.

| Kriter | Durum | Nerede kanıtlandı |
|---|---|---|
| AC-1 can düşer ve yenilenir | ✅ | `RegeneratingHealthTests.AC1_*` (4 test) |
| AC-2 yeni hasar yenilenmeyi sıfırlar | ✅ | `AC2_YenilenirkenGelenHasar_GecikmeyiBastanBaslatir` |
| AC-3 sıfır can run'ı bir kez bitirir | ✅ | `AC3_*` (3 test) + `RunSignalsTests.AC3_*` |
| AC-4 skor ekranı doğru sayıları gösterir | ✅ | `RunRecorderTests.AC4_*` (3 test) |
| AC-5 R yeni run başlatır | ✅ (kod) | `AC5_*` — **temiz ikinci run oyun testinde doğrulanacak** |
| AC-6 sayılar sızmaz | ✅ | `AC6_*` (3 test) |
| AC-7 ölüm sürpriz değil | ✅ (kod) | `AC7_*` — **okunabilirliği oyun testinde** |

**Testler:** 212/212 yeşil (177 → +35). **Derleme:** temiz. **Config:** PASS (7 dosya).

**CR-CODE: CONDITIONAL → düzeltildi.** Bulgular ve yapılanlar:

| Ağırlık | Bulgu | Düzeltme |
|---|---|---|
| BLOCKER | AC-3 yarım: girdi yalnızca `PlayerController`'da kesilmişti; ateş, bıçak, tamir ve satın alma skor ekranının arkasında çalışmaya devam ediyordu | Dördüne de `RunSignals.IsRunOver` kapısı eklendi |
| MAJOR | Skor metni her `OnGUI`'de yeniden kuruluyordu (kare başına birden fazla tahsis) | Metin `OnRunEnded`'da bir kez kuruluyor |
| MINOR | `ZombieDirector.OnRunRestarted` otorite kontrolü yapmıyordu | `_authoritative` kontrolü eklendi |
| NOTE | Üç bileşen `OnStartServer`/`OnStopServer` kullanıyor, `OnEnable`/`OnDisable` değil | Bilerek — üçünün işi de otorite işi. Gerekçe `RunSignals` doc'una yazıldı |

**Oyun testinde bakılacaklar:**

1. Öl → skor ekranındaki tur, süre, öldürme sayısı gerçekten oynanana uyuyor mu?
2. R → sahada zombi yok, can tam, puan sıfır, **kapılar tekrar kilitli**, oyuncu başlangıçta mı?
3. Düşük can uyarısı, ölmeden **önce** görülüyor mu (AC-7'nin asıl sorusu)?
4. 4 sn kaçınca canın dolması bir ödül gibi mi, yoksa ölümü anlamsız mı kılıyor?

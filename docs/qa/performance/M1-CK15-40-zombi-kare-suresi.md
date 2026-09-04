# ÇK-15 — 40 zombi ile kare süresi

| Alan | Değer |
|---|---|
| **Kriter** | M-01 ÇK-15: *"Kare süresi 40 zombi ile bütçede"* |
| **Sonuç** | ✅ **Bütçede** — p99 bütçenin **%8.5**'i |
| **Ölçen** | `PerfRunner` (otomatik), `Bunker.AI` |
| **Nerede** | **Build** (editör değil), gerçek harita NavMesh'i |
| **Tarih** | 2026-09-04 |
| **Ham veri** | `evidence/perf-round6.json`, `perf-round12.json`, `perf-round20.json` |

---

## Sonuç

Bütçe: **16 ms** (60 FPS, `PERF-BUDGET.md`).

| Tur | Sahada zombi (ort / max) | p50 | p99 | p99 / bütçe | Ort. FPS |
|---|---|---|---|---|---|
| 6 | 13.3 / 14 | 0.836 ms | 1.312 ms | **%8.2** | 1151 |
| 12 | 23.0 / 24 | 0.861 ms | 1.343 ms | **%8.4** | 1120 |
| **20** | **39.2 / 40** | **0.892 ms** | **1.361 ms** | **%8.5** | 1088 |

Üç koşunun üçünde de ölçüm penceresi **eksiksiz** (`windowComplete: true`), 40 saniye,
~44.000 kare örneği.

### Marjinal maliyet ihmal edilebilir

14 → 40 zombi (26 zombi daha): p50 **0.836 → 0.892 ms**.

**Zombi başına ~0.002 ms.** M0-04'ün bulgusunu doğruluyor: büyüme doğrusal ve marjinal
maliyet düşüyor; **40 agent'a kadar diz noktası yok.** 40 zombi, kare bütçesinin
yaklaşık **%0.35**'ini yiyor.

---

## Bu ölçümün söylemedikleri

Bir ölçümün ne ölçmediğini söylememesi, onu yanlış ölçmekten daha tehlikelidir.

| Kapsam dışı | Neden önemli |
|---|---|
| **Animator yok** | Gri kutu zombileri kapsül; animasyon yok. `PERF-BUDGET.md` Animator'ı **beklenen asıl risk** olarak işaretliyor ve hâlâ ölçülmedi. Bu sonuç o riski **azaltmıyor** |
| **Geliştirme makinesi** | Bütçe dokümanı açık: *"Geliştirme makinesi ölçüt değildir."* 1088 FPS orta seviye bir işlemcide hiçbir şey ifade etmez — **bütçe oranı** ölçeklenebilir olan tek sayı |
| **1600×900 pencereli** | Hedef 1080p. GPU yükü bu haritada zaten gürültü sınırında, ama ölçüm 1080p'de tekrarlanmadı |
| **Oyuncu ateş etmiyor** | Isın testi, hasar uygulaması, zombi ölümü ve havuza dönüş ölçüm dışında. Bunlar seyrek olaylar; kare süresini süren şey sürekli koşan yol bulma ve çizim |
| **Ağ yok** | Tek kişilik host, uzak istemci sıfır. Ağ serileştirmesi M-02'nin kalemi |

**Dürüst okuma:** *40 agent'ın CPU yükü bu oyunun riski değil.* Zaten M0-04 bunu ima
ediyordu; bu koşu gerçek harita ve build üzerinde doğruladı. Risk hâlâ animasyonda ve
o ölçüm ancak animasyonlu model geldiğinde yapılabilir.

---

## Nasıl tekrarlanır

```bash
powershell -NoProfile -File .claude/tools/build.ps1 -Target StandaloneWindows64 -Config Development -Method Bunker.Editor.BuildPipelineEntry.BuildFromArgs
```

Sonra:

```bash
Build/windows64-development/Bunker.exe -perfRun -perfRound 20 -perfSeconds 40 -perfOut rapor.json
```

| Argüman | Ne yapar |
|---|---|
| `-perfRun` | Ölçümü açar. **Bu argüman olmadan `PerfRunner` hiç uyanmaz** — normal oyuna sıfır risk |
| `-perfRound N` | Hangi tura atlanacağı. 40 eşzamanlı zombi için **20** (tur 12 yalnızca 24 üretir; `maxConcurrent: 40`'a ~18. turda ulaşılır) |
| `-perfSeconds S` | Ölçüm penceresi. Öncesinde 6 sn ısınma var ve ısınma ölçüme **girmez** |
| `-perfOut yol` | Rapor dosyası |

Koşu bitince oyun kendini kapatır.

### Ölçüm rigi nasıl çalışıyor

**Yem hedef.** Yerde duran bir oyuncu dört vuruşta ölür, run biter ve ölçüm penceresi
hiç açılmaz. `ZombieAgent` hasarı **beacon üzerinden** verir (`_target.ReceiveAttack`),
yani `IDamageable`'ı olmayan bir hedef hiç hasar almaz. Koşu, oyuncunun durduğu yere
hasar almayan bir yem beacon koyar ve oyuncunun kendi beacon'ını kapatır. Zombiler
normal şekilde kovalar, yol bulur, toplanır ve saldırır — kimse ölmez.

Oyuncuya **dokunulmuyor**: konumunu yazmaya çalışmak `CharacterController` açıkken
sessizce işe yaramazdı (BUG-005'in birebir aynısı).

---

## Kurarken çıkan iki ölçüm hatası

Kayda değer, çünkü ikisi de **sessizce yanlış sayı üretiyordu**:

1. **Halka tampon 4096 sabitti.** 45 saniyelik bir koşuda ~50.000 kare oluyor; tampon
   eskisinin üstüne yazdığı için yalnızca **son ~3.4 saniye** ölçülüyordu. p99'un bütün
   amacı seyrek takılmaları yakalamak, ve üç saniyelik bir pencerede seyrek olan şey hiç
   görünmez. Tampon artık pencerenin tamamını alıyor ve rapor `windowComplete` ile
   bunu **doğruluyor**.
2. **Oyuncu `Start()`'ta aranıyordu.** Oyuncu Mirror tarafından ağ başladıktan sonra
   doğuyor; ilk koşu "oyuncu bulunamadı" deyip çıktı. Artık doğana kadar bekleniyor
   (30 sn tavan).

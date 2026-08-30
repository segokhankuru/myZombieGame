# M0-04 — NavMesh agent yükü ölçümü

**Tarih:** 2026-08-30 · **İş:** M0-04 · **Tip:** Infra
**Karşılanan kriter:** **ÇK-5** — host kare süresi < 16 ms, 40 agent aktifken
**Ortam:** Unity 6000.3.23f1 Editor, Mirror host modu, `M0-Sandbox`, düz zemin

---

## Ölçüm yöntemi

`AgentLoadTest` otomatik taraması (F5): her adımda 3 sn oturma + 15 sn ölçüm.
Örnekler `FrameTimeRecorder` halka tamponunda toplandı, yüzdelikler sıralanmış
kopyadan hesaplandı.

Agent'lar rastgele hedeflere yürüyen NavMesh agent'ları. **Zombi değiller:**
animator yok, algı yok, saldırı yok, can yok, oyun mantığı yok.

---

## Sonuçlar (milisaniye)

### Çizim kapalı — saf CPU

| agent | p50 | p95 | p99 | tabana göre fark (p50) |
|---|---|---|---|---|
| 0 | 1.45 | 1.83 | 2.24 | — |
| 40 | 1.62 | 1.93 | 2.11 | **+0.17** |
| 100 | 1.77 | 2.12 | 2.38 | +0.32 |
| 200 | 1.91 | 2.28 | 2.60 | +0.46 |

### Çizim açık

| agent | p50 | p95 | p99 | tabana göre fark (p50) |
|---|---|---|---|---|
| 0 | 1.41 | 1.66 | 1.82 | — |
| 40 | 1.65 | 1.96 | 2.18 | **+0.24** |
| 100 | 1.77 | 2.05 | 2.22 | +0.36 |
| 200 | 1.96 | 2.23 | 2.41 | +0.55 |

**`max` sütunu rapora alınmadı.** Sıfır agent'ta bile 13–16 ms sıçramalar gösteriyordu;
bunlar editör kaynaklı (GC, pencere odağı, arka plan derlemesi) ve ölçülen şeyle ilgisiz.
Kuyruk metriği olarak p99 kullanıldı.

---

## Verdikt: ÇK-5 GEÇTİ

**Kırk agent kare süresine ~0.2 ms ekliyor — 16 ms bütçesinin yaklaşık %1.2'si.**

İki yorum notu:

**Ölçek bağımsız okuma.** Bu makinedeki mutlak FPS (~600) hedef donanımı temsil etmiyor;
`docs/CONTEXT.md` bunu zaten söylüyor. Anlamlı olan bütçe oranı. Üç kat yavaş bir işlemcide
40 agent ~0.6 ms yer, yani bütçenin %4'ü. Beş kat yavaşta ~1 ms, %6. Her durumda güvenli.

**Diz yok.** 200 agent'a kadar büyüme doğrusal ve marjinal maliyet düşüyor
(4.3 → 3.1 → 2.3 µs/agent). Yol isteği kuyruğunun tıkandığı bir kırılma noktası
görülmedi. 40 hedefi için geniş baş payı var.

**Çizim maliyeti ihmal edilebilir.** Kırk kapsül açık/kapalı farkı 0.03 ms — gürültü
sınırında. Beklenen: dokusuz kapsüller pratikte bedava.

---

## Bu ölçümün KAPSAMADIĞI şeyler

Verdikt yalnızca ölçülen şey için geçerlidir. Ölçülmeyenler:

| Ölçülmedi | Neden önemli |
|---|---|
| **Animator** | Gerçek zombilerde Animator olacak. Kalabalık oyunlarında animator maliyeti sık sık NavMesh'ten **büyüktür**. `docs/CONTEXT.md` yükün bir parçası olarak "~40 animator" diyor; biz 40 agent'ın **sıfırında** animator ölçtük |
| **Ağ serileştirme** | Solo host modunda uzak istemci yok, dolayısıyla serileştirme maliyeti sıfır. M-02'de gelecek |
| **Oyun mantığı** | Algı, can, hasar, isabet tepkisi, saldırı, spawn/despawn dönüşümü |
| **Gerçek harita** | Düz bir zemin, mümkün olan en ucuz NavMesh. Üç bölgeli iki katlı bina, kapılar ve engellerle daha karmaşık yollar üretecek |
| **Build** | Ölçüm editörde yapıldı. Build genelde daha hızlıdır, yani bu taraf muhafazakâr |

### Ölçümü muhafazakâr yapan bir hata

Tarama sırasında `AgentLoadTest`'in yol isteme hızı **kare hızına bağlıydı** (60 FPS
varsayımıyla hesaplanıyordu). Oyun ~600 FPS'te koştuğu için agent'lar hedeflenenin
**on katı sıklıkta** yol istedi — 0.4 sn yerine 0.04 sn'de bir.

Yani yukarıdaki sayılar, gerçekte olacağından belirgin şekilde **kötü** bir senaryoyu
ölçüyor. Verdikti zayıflatmıyor, güçlendiriyor. Hata ölçümden sonra düzeltildi
(`_repathsPerSecond`, `Time.unscaledDeltaTime` ile ölçekleniyor).

---

## Bu ölçümün asıl bulgusu

> **NavMesh yol bulma bu oyunun darboğazı değil. Risk animator ve ağ serileştirmesine kaydı.**

M-01 için doğrudan sonuç: zombi sayısı endişesi NavMesh tarafında değil. İzlenecek şey
**animator maliyeti**. ADR-0003 zaten önlemi yazıyor — 15 m üstündeki zombilerde
`Animator.cullingMode = CullCompletely`. Bu artık bir tercih değil, ölçümün işaret ettiği
öncelik.

M-02 için: ağ serileştirmesi ölçülmemiş tek büyük kalem olarak duruyor ve zaten o
milestone'un konusu.

---

## Yeniden üretme

1. `M0-Sandbox` sahnesini aç, Play
2. `R` ile çizimi kapat
3. `F5` — tarama ~72 saniyede biter, tabloyu konsola basar
4. `R` ile çizimi aç, `F5` ile tekrarla

Tarama adımları `AgentLoadTest` üzerindeki `sweepCounts` alanından değiştirilebilir.

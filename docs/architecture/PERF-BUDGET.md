# Performans Bütçesi

**Sahibi:** performance-engineer · **Son güncelleme:** 2026-08-30
**İlgili:** ADR-0003 (URP), M-00 ÇK-5

---

## Hedef

| | |
|---|---|
| Çözünürlük | 1080p, üstü açık (kalite ayarları ve çözünürlük ölçeği ile) |
| Kare hızı | **60 FPS → 16 ms kare bütçesi** |
| Donanım | Orta seviye genel donanım. **Geliştirme makinesi ölçüt değildir** |
| Eşzamanlı zombi | ~40 |

Ölçümler her zaman **bütçe oranı** olarak okunur, mutlak FPS olarak değil. Geliştirme
makinesindeki 600 FPS, orta seviye bir işlemcide hiçbir şey ifade etmez; "40 agent
bütçenin %1.2'sini yiyor" ise ölçeklenebilir bir cümledir.

---

## Darboğaz nerede

**GPU değil, CPU.** Kapalı ve küçük bir harita URP'de neredeyse bedava. Yükü üreten üç
kalem var:

1. Animator (~40 adet)
2. NavMesh yol bulma ve hareket (~40 agent)
3. Ağ serileştirmesi (host, 3 istemciye)

Kare süresi düştüğünde önce Profiler'da bu üçüne bakılır, çözünürlüğe değil.

---

## Ölçülen

| Kalem | Maliyet | Bütçe oranı | Kaynak |
|---|---|---|---|
| 40 NavMesh agent (animatörsüz) | **~0.2 ms** | %1.2 | `docs/qa/performance/M0-04-navmesh-agent-yuku.md` |
| 200 NavMesh agent (animatörsüz) | ~0.5 ms | %3 | aynı |
| 40 kapsül çizimi | ~0.03 ms | gürültü sınırı | aynı |
| Boş sahne tabanı (editör, host modu) | ~1.4 ms | %9 | aynı |

40 agent'a kadar **diz noktası görülmedi**; büyüme doğrusal ve marjinal maliyet düşüyor.

---

## Ölçülmemiş — açık kalemler

Bunlar bütçenin bilinmeyen tarafı ve sırayla kapatılacak:

| Kalem | Ne zaman ölçülecek | Neden önemli |
|---|---|---|
| **Animator (40 adet)** | M-01 | Kalabalık oyunlarında sık sık NavMesh'ten pahalıdır. Ölçümün asıl bulgusu riskin buraya kaydığıydı |
| **Ağ serileştirmesi** | M-02 | Host, 3 istemciye 40 zombi gönderiyor |
| Oyun mantığı (algı, hasar, isabet) | M-01 | — |
| Gerçek harita NavMesh'i | M-01 | Düz zemin en ucuz durumdur |
| Build (editör değil) | M-01 sonu | Editör muhafazakâr taraf, ama doğrulanmalı |

---

## Zorunlu önlemler

ADR-0003'ten ve M0-04 bulgusundan:

- 15 m üstündeki zombilerde `Animator.cullingMode = CullCompletely` — **ölçümün işaret
  ettiği öncelik, tercih değil**
- Baked lighting + ışık probe'ları; her zombide gerçek zamanlı gölge veren ışık yok
- Occlusion culling, GPU instancing, tek atlas
- Her ağ nesnesi pool'dan
- Yol istekleri kısıtlı ve kareler arasına yayılmış (`AgentLoadTest` bunun referansı)

---

## Ölçüm araçları

| Araç | Ne için |
|---|---|
| `PerfHud` (F1/F2) | Oynarken canlı kare süresi, p50/p99, elle ölçüm oturumu |
| `AgentLoadTest` (F5) | Otomatik yük taraması, agent sayısına göre tablo |
| Unity Profiler | **Yetkili kaynak.** Kanıt dosyalarına giren sayı buradan alınır |
| `FrameTimeRecorder` | Yüzdelik hesabı, saf C#, EditMode testli |

`PerfHud` ve `AgentLoadTest` kare başına tahsis yapmaz — ölçüm aracı ölçümü bozmamalı.

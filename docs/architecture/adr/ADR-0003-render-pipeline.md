# ADR-0003: Render pipeline

**Status:** Accepted
**Date:** 2026-08-29 | **Approval:** kullanıcı
**Reversal cost:** high

## Context

Ton: loş, dar iç mekân; ışık kontrastı atmosferin ana taşıyıcısı (`design/00-brief.md`).
Hedef: 1080p/60 **genel donanımda**, daha yüksek çözünürlük isteyene açık. Aynı anda ~40
zombi + 4 oyuncu.

Kritik gözlem: bu oyunda **darboğaz GPU değil CPU.** Kapalı, küçük bir harita URP'de
neredeyse bedava; asıl yükü 40 NavMesh agent'ı, 40 animator ve ağ senkronu üretiyor. Yani
pipeline seçimi görsel kalite için yapılır, kare hızı için değil — kare hızı CPU
tarafında kazanılır veya kaybedilir.

## Options considered

| Option | Pros | Cons | Why eliminated |
|---|---|---|---|
| Built-in | Tanıdık, bol eski öğretici | Unity tarafından geliştirilmiyor, yeni asset'ler URP/HDRP hedefliyor | Elendi |
| HDRP | En güçlü ışık, hacimsel sis, ray tracing — loş mekân tonuna cazip geliyor | Unity 2026 stratejisinde **bakım moduna** alındı (yalnızca stabilite ve regresyon). Düşük/orta donanımda ağır, Steam Deck sınıfı zorlanır. Solo geliştirici için ayar yüzeyi çok geniş | Elendi — atmosferi URP'de de kurabiliriz, karşılığında donanım tabanını kaybetmeye değmez |
| **URP (Forward+)** | Unity 2026'da **aktif geliştirilen** pipeline. Stilize ve okunabilir siluet odaklı yapıma uygun. Geniş donanım tabanı + Steam Deck. Dinamik GI, SSR, fiziksel ışık birimleri artık URP'de | HDRP'nin hacimsel derinliği ve ray tracing'i yok | **Seçildi** |

## Decision

**URP (Forward+) kullanacağız.**

## Consequences

**Positive:** Geniş donanım tabanı; Steam Deck yolu açık. Satın alınacak asset'lerin
çoğunluğu URP hedefliyor. Unity'nin geliştirmeye devam ettiği taraftayız.

**Cost we are accepting:** Hacimsel ışık ve ray tracing yok. Loş mekân atmosferi baked
lighting, ışık probe'ları, sis ve post-processing ile kurulacak — bu daha fazla **sanat
yönü işi**, daha az motor işi. Kabul ediyoruz, çünkü atmosferi taşıyan şey zaten kontrast
ve ses, hacimsel ışık değil.

**Reversal cost:** **high** — pipeline değişimi her materyali, shader'ı ve ışık ayarını
etkiler. Sanat geçişi (M3) başladıktan sonra pratikte geri dönülemez.

## Implementation guidance

**Required pattern:** Forward+ · baked lighting + ışık probe'ları · occlusion culling ·
GPU instancing · tek atlas · 15 m üstündeki zombilerde `Animator.cullingMode = CullCompletely`
**Forbidden pattern:** Her zombide gerçek zamanlı gölge veren ışık. Kare başına açılan
yeni materyal örneği (`renderer.material` — `sharedMaterial` kullan).
**Lives in:** `Assets/_Project/Settings` (pipeline asset'leri ve kalite seviyeleri)
**Watch out for:** Kare hızı düştüğünde çözünürlük veya gölge kalitesiyle oynamak. Bu
oyunda düşüş büyük ihtimalle **CPU** kaynaklıdır — önce Profiler'da AI ve animator'a bak.

## Verification

Kalite seviyeleri (Düşük/Orta/Yüksek) ve dahili çözünürlük ölçeği en geç M3'te
tanımlanır. `PERF-BUDGET.md` CPU tarafını ayrıca bütçeler.

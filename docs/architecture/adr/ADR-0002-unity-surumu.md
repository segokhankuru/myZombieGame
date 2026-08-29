# ADR-0002: Unity sürümü

**Status:** Accepted
**Date:** 2026-08-29 | **Approval:** kullanıcı
**Reversal cost:** medium

## Context

Solo geliştirici, 12+ ay sürecek bir proje, Steam yayını hedefi. Sürüm seçiminde tek
kriter özellik değil: **projenin ömrü boyunca desteklenmeye devam etmesi.** Sürüm
yükseltmesi solo projede günler yiyen ve hiçbir oyuncunun görmediği bir iştir.

## Options considered

| Option | Pros | Cons | Why eliminated |
|---|---|---|---|
| Unity 6.0 LTS | Olgun, çok test edilmiş | Desteği **Ekim 2026'da bitiyor** — proje daha yayınlanmadan | Elendi |
| Unity 6.4 | Daha yeni | LTS değildi, **Haziran 2026'da EOL oldu**, güvenlik yaması almıyor | Elendi |
| Unity 6.7 LTS (yıl sonu bekleniyor) | En uzun destek penceresi | Henüz yok. Beklemek M0'ı geciktirir; çıkınca yükseltmek LTS içi olduğu için ucuz | Ertelendi, elenmedi |
| **Unity 6.3 LTS** | **Aralık 2027'ye kadar destekli** — projenin tamamını kapsıyor. Unity'nin "üretime kilitlenecek projeler" için kendi tavsiyesi. FishNet Unity 6 API'sini destekliyor | Yıl sonunda 6.7 LTS çıkınca bir sürüm geride kalır | **Seçildi** |

## Decision

**Unity 6.3 LTS (6000.3.x) kullanacağız.**

## Consequences

**Positive:** Destek penceresi projenin tamamını kapsıyor. Unity Personal ücretsiz
(yıllık gelir eşiğinin çok altındayız). FishNet ve Multiplayer Play Mode ile uyumlu.

**Cost we are accepting:** 6.7 LTS çıktığında bir LTS geride kalacağız. Bu bilinçli —
LTS ortasında sürüm değiştirmek özellik kazandırmaz, hafta kaybettirir.

**Reversal cost:** **medium** — LTS içi yamalar ucuz, LTS'ler arası geçiş bir haftalık
regresyon testi demektir.

## Implementation guidance

**Required pattern:** Editör sürümü `ProjectSettings/ProjectVersion.txt` ile kilitlenir.
Ekip (sen + testçi arkadaşlar) **aynı yama sürümünü** kullanır.
**Forbidden pattern:** "Unity Hub yeni sürüm önerdi" diye üretim ortasında yükseltmek.
**Watch out for:** Yama sürümü farkı, `.unity` ve `.prefab` dosyalarında sahte diff üretir.

## Verification

`ProjectVersion.txt` git'te takip edilir; sürüm değişikliği ayrı bir commit ve bu ADR'ye
bir güncelleme gerektirir.

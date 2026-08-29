# ADR-0002: Unity sürümü

**Status:** Accepted
**Date:** 2026-08-29 | **Approval:** kullanıcı
**Reversal cost:** medium

## Context

Solo geliştirici, 12+ ay sürecek bir proje, Steam yayını hedefi. Sürüm seçiminde tek
kriter özellik değil: **projenin ömrü boyunca desteklenmeye devam etmesi.** Sürüm
yükseltmesi solo projede günler yiyen ve hiçbir oyuncunun görmediği bir iştir.

## Sürüm ömürleri (ölçülmüş veri, 2026-08-29)

| Sürüm | Çıkış | Destek bitişi | LTS | Yaşadığı süre |
|---|---|---|---|---|
| 6000.5 | 2026-06-15 | henüz atanmamış | Hayır | — |
| 6000.4 | 2026-03-18 | 2026-06-17 | Hayır | **3 ay** |
| **6000.3** | 2025-12-04 | **2027-12-04** | **Evet** | **24 ay** |
| 6000.2 | 2025-08-12 | 2025-12-04 | Hayır | **4 ay** |
| 6000.1 | 2025-04-23 | 2025-08-12 | Hayır | **4 ay** |
| 6000.0 | 2024-04-29 | 2026-10-16 | Evet | 30 ay |

**Desen açık: LTS olmayan sürümün desteği, bir sonraki sürüm çıktığı gün bitiyor.**
Unity bunlara "update release" diyor ve üretim kalitesinde olduklarını belirtiyor — bu
doğru; ayrım kalitede değil, **yama alma süresinde**.

## Options considered

| Option | Pros | Cons | Why eliminated |
|---|---|---|---|
| Unity 6.0 LTS | Olgun, çok test edilmiş | Desteği **Ekim 2026'da bitiyor** — proje daha yayınlanmadan | Elendi |
| Unity 6.4 | Daha yeni | LTS değildi, **Haziran 2026'da EOL oldu** (3 ay yaşadı) | Elendi |
| Unity 6.5 | Geliştiricinin makinesinde **zaten kurulu** — sıfır kurulum maliyeti, en güncel özellikler | LTS değil. 6000.6 çeyreklik kadansa göre Eylül 2026'da bekleniyor; o gün 6.5 yama almayı bırakır. 15+ aylık projede bu, her çeyrek bir sürüm yükseltmesi ve her seferinde bir haftalık regresyon testi demek — oyuncunun göreceği hiçbir şey kazandırmadan | Elendi. Kurulum tasarrufu, tekrarlayan yükseltme maliyetinin yanında önemsiz |
| Unity 6.7 LTS (yıl sonu bekleniyor) | En uzun destek penceresi | Henüz yok. Beklemek M0'ı geciktirir; çıkınca yükseltmek LTS içi olduğu için ucuz | Ertelendi, elenmedi |
| **Unity 6.3 LTS (6000.3.23f1)** | **Aralık 2027'ye kadar destekli** — projenin tamamını kapsıyor. Unity'nin "üretime kilitlenecek projeler" için kendi tavsiyesi. URP, NavMesh ve Multiplayer Play Mode mevcut | Yıl sonunda 6.7 LTS çıkınca bir sürüm geride kalır | **Seçildi** |

## Decision

**Unity 6.3 LTS (6000.3.x) kullanacağız.**

## Consequences

**Positive:** Destek penceresi projenin tamamını kapsıyor. Unity Personal ücretsiz
(yıllık gelir eşiğinin çok altındayız). Multiplayer Play Mode ile uyumlu. Mirror uyumluluğu M0-01'de doğrulanacak (belgesi 6000.1'e kadar yazıyor).

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

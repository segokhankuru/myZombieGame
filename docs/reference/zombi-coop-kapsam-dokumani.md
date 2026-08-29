# Proje Kapsam Dokümanı — v2.0
## 4 Kişilik Co-op, Tur Bazlı Zombi Hayatta Kalma FPS'i

> **Değişiklik:** v1.0 profesyonel bir stüdyo ekibi varsayıyordu. v2.0 **tek geliştirici + Claude Code + arkadaş testçileri + satın alınan asset'ler** gerçeğine göre baştan yazıldı.
>
> **Ekip:** 1 geliştirici (sen) + 3 arkadaş (testçi, ara sıra yardım)
> **Araç:** Unity 6.3 LTS + Claude Code
> **Platform:** Steam / Windows (+ Steam Deck)
> **Hedef fiyat:** $12–15

---

## 1. Gerçeklik Kontrolü — Önce Bunu Oku

Solo geliştirmede projeleri öldüren şey yetenek değil, **kapsam**. v1.0'daki hedefler (3 harita, 22 silah, 8 zombi tipi, 80 kart) 5 kişilik bir ekibin 18 aylık işiydi. Tek başına o kapsamı denersen 2 yıl sonra yarı bitmiş bir projeyle kalırsın — bu türün mezarlığı bu şekilde doldu.

**Bu projenin gerçek riski netcode değil, bitirememek.**

Revize hedef: **1 harita, 12 silah, 5 zombi tipi, 45 kart ile yayınla.** Oyun tutarsa ücretsiz güncellemelerle büyüt. Steam algoritması zaten yayın sonrası güncellemeleri ödüllendiriyor, bu yüzden az içerikle çıkıp büyümek stratejik olarak da doğru.

Karşılaştırma için: *Vampire Survivors* tek geliştiriciyle, tek haritayla, satın alınmış asset'lerle çıktı. İçerik miktarı değil, döngünün kalitesi belirledi.

### Değişen ne?

| | v1.0 (stüdyo) | v2.0 (solo) |
|---|---|---|
| Harita (yayında) | 3 | **1** (+1 ücretsiz güncelleme) |
| Silah | 22 | **12** |
| Zombi tipi | 8 + 3 boss | **5 + 1 boss** |
| Kart | 80 | **45** |
| Stim/perk sistemi | 8 adet | **Silindi** — kart havuzuna gömüldü |
| Silah modülleri | Ayrı sistem | **Silindi** — kart oldu |
| Bot desteği | Var | **Silindi** |
| Yerelleştirme | 10 dil | **3 dil** (TR, EN, RU) |
| Meta ilerleme | 30 seviye | **12 seviye** (sadece kart havuzu açar) |
| Zaman | 18 ay / 5 kişi | **10–16 ay / 1 kişi** |
| Bütçe | $200k+ | **$2.000–6.000** |

---

## 2. Fikri Mülkiyet Sınırları

Mekanik serbest, kimlik değil. Tur bazlı zombi hayatta kalma bir tür — kimsenin tekelinde değil. Ama şunlar Activision'ın:

**YASAK:** "Pack-a-Punch", "Juggernog", "Mystery Box", "Perk-a-Cola", "Nacht der Untoten" gibi isimler · birebir harita düzenleri · orijinal ses efektleri ve jingle'lar · kırmızı gizemli kutu ve ayı oyuncağı gibi görsel imzalar · karakterler, replikler, Easter egg zincirleri · "Nazi Zombies" markalı kullanım.

**SERBEST:** Tur yapısı · pencereden gelen zombiler · barikat tamiri · puanla kapı açma · duvarda silah satın alma · rastgele silah dağıtıcı · yerde düşen güçlendirmeler · diriltme mekaniği.

Kendi isimlendirmen:
- Yükseltme istasyonu → **FORGE-9 Tezgâh**
- Rastgele silah kutusu → **Bozuk Otomat**
- Perk sistemi → *(silindi, kart oldu)*

**Silah isimleri:** Gerçek silah *modelleri* serbest ama *isimler* tescilli (Glock, H&K, Colt). Satın aldığın asset paketleri genelde jenerik isim kullanır — yine de kontrol et, gerekirse yeniden adlandır.

**Aksiyon:** Yayından ~2 ay önce bir oyun hukuku avukatıyla 1 saatlik danışma (~$200–500). Solo bütçede bile bu kalemi kesme.

---

## 3. Çekirdek Oyun Sistemleri

### 3.1 Ekonomi (tek para birimi — bölme)
- İsabet +10 · Öldürme +60 · Kafa vuruşu +100 · Bıçak +130 · Barikat tahtası +10
- Puan **hem para hem skor**. İkinci bir para birimi ekleme.
- Ölüm cezası: mevcut puanın %50'si (kart kaybı YOK)

### 3.2 Tur Yapısı
- Tur 1: oyuncu başına ~6 zombi
- Can: Tur 1–9 doğrusal, Tur 10+ tur başına %10 çarpan, ~Tur 55'te tavan
- Hız: 1–4 yürüyüş · 5–8 tempolu · 9+ koşu
- Turlar arası 10 sn nefes molası

### 3.3 Kart Draft Sistemi (ana farklılaştırıcı)
Bu sistem, oyunun 20. turdan sonra neden oynanmaya devam edeceğinin cevabı. Aynı zamanda eski perk + silah modülü sistemlerinin yerini alıyor.

- **Her 3 turda bir** (tur 3, 6, 9...) + boss sonrası garanti nadir kart
- 3 karttan 1 seçim, **4 oyuncu eşzamanlı**, 10 sn timer, süre dolunca otomatik
- Yeniden çekme: run başına 1 ücretsiz, sonrası puanla
- **Kartların en az yarısı kural değiştirmeli, salt istatistik olmamalı**

| Zayıf kart | Güçlü kart |
|---|---|
| +%10 hasar | Öldürdüğün zombi patlar |
| +%15 hız | Kayarken hasar almazsın, zombileri devirirsin |
| +20 mermi | Şarjör yok, sürekli dolum ama %40 yavaş ateş |
| +%5 can | Düşünce 3 sn içinde 5 kill = kendini kaldır |

- **Etiket sistemi:** `Yanıcı` `Elektrik` `Kan` `Hız` `Beden`. Aynı etiketten 3 kart = bonus açılır → build kimliği
- **Takım kartları:** nadir, 4 oyuncuyu birden etkiler ("kim alacak?" sosyal anı)
- İçerik: MVP 25 kart → yayında **45 kart**

### 3.4 Zombi Drop Sistemi

| Drop | Şans | Etki |
|---|---|---|
| Hiçbir şey | %82 | — |
| Puan yığını | %8 | Anlık, cüzdana |
| Mermi kutusu | %4 | Eldeki silahı doldurur |
| Can paketi | %3 | 4m yarıçapta iyileştirir |
| Geçici buff | %2 | 15 sn (hız / ateş hızı / hasar) |
| Nadir güçlendirme | %1 | Maks Mermi, Çift Puan, Anında Öldürme, Alan Temizleme |

**Kurallar:**
- **Akıllı drop:** takımın canı düşükse can şansı 2x, mermisi bitmişse mermi 2x. Oyuncu fark etmez, "tam zamanında" hissi verir
- **Tüm buff'lar geçici.** Kalıcı güç kartların işi — karıştırma
- **Coin drop yok.** Zaten kill = puan
- **Drop öldürmeye bağlı, girişe değil.** "Camdan giren drop bırakır" dersen kimse barikat tamir etmez, +10 puan ekonomisi ölür
- Performans: aynı anda maks 15 drop, 20 sn ömür, 2.5m mıknatıs yarıçapı, tamamı pool'dan

### 3.5 Tur Modifikatörleri
Her 5 turda bir, oyuncular 3 seçenekten oylar. **6 adet yeterli** (v1'de 8 yazıyordu, kesildi):
Sisli Tur · Düşük Yerçekimi · Yanıcı Sürü · Kafa Avı · Koşucu Turu · Karaborsa

### 3.6 Silah Yükseltme (FORGE-9)
3 kademe: hasar / mermi kapasitesi / ateş hızı. Ayrı modül sistemi **yok** — modüller kart oldu.

### 3.7 Sadeleşmiş Sistem Haritası
Altı sistem yerine dört:

| Sistem | Ne verir | Ne zaman |
|---|---|---|
| Ekonomi | Silah, kapı, tuzak, yükseltme | Sürekli |
| Kartlar | Kalıcı güç + build kimliği | 3 turda bir |
| Drop'lar | Anlık, geçici güç | Rastgele |
| Meta ilerleme | Kart havuzunu genişletir | Seanslar arası |

---

## 4. İçerik Kapsamı

| Varlık | MVP | Yayın |
|---|---|---|
| Harita | 1 (gri kutu) | 1 (tam) |
| Harita bölgesi | 4 | 6–7 |
| Silah | 6 | 12 |
| Zombi tipi | 3 | 5 |
| Boss | 0 | 1 |
| Wonder Weapon | 0 | 1 |
| Kart | 25 | 45 |
| Güçlendirme | 4 | 4 |
| Tur modifikatörü | 3 | 6 |
| Harita tuzağı | 2 | 4 |
| Müzik | 2 | 6 |
| Steam başarımı | — | 20 |
| Dil | TR/EN | TR/EN/RU |

**Zombi tipleri (5):** Standart · Koşucu · Zırhlı · Patlayıcı · Tanker(mini-boss)

**Silah dağılımı (12):** 2 tabanca · 3 tüfek · 2 pompalı · 2 SMG · 1 keskin nişancı · 1 ağır · 1 wonder weapon

---

## 5. Asset Stratejisi — Satın Alma Varsayılan, Son Çare Değil

Solo geliştirmede asset üretmek en büyük zaman tuzağı. Doğru yaklaşım: **satın al, üstünü kendi ışık/materyal geçişinle birleştir.**

### 5.1 Sanat Yönü Kararı: Stilize Git

Gerçekçi modern sanat yönü solo geliştiricinin projesini öldürür — tutarlılık için sanatçı seviyesinde iş gerekir. **Stilize (low-poly / cel-shaded) yaklaşım:**
- Satın alınabilir, tutarlı asset kütüphaneleri var (Synty POLYGON serisi gibi)
- Steam Deck'te rahat 60 FPS
- Solo geliştiricinin sanat zayıflığını gizler, hatta stil olarak okunur
- "Modern gün" kurgusunu bozmaz — stilize modern şehir gayet işler

Karar: **Stilize modern.** Bu kararı erken ver ve değiştirme.

### 5.2 Satın Alma Listesi

| İhtiyaç | Yaklaşım | Tahmini |
|---|---|---|
| Çevre kit (modüler bina, sokak, prop) | Paket satın al | $60–150 |
| Silah modelleri **+ animasyonlar** | Paket satın al — **animasyonlu paket al**, tek başına model alma | $80–200 |
| Zombi modelleri + animasyon | Paket satın al, materyal geçişiyle 5 varyant çıkar | $50–150 |
| Karakter (FPS kol/el) | Paket | $30–80 |
| SFX kütüphanesi | Paket (silah, zombi, UI, ortam) | $100–250 |
| Müzik | Royalty-free lisans veya freelance | $150–600 |
| VFX (kan, ateş, patlama) | Paket | $40–100 |
| UI kit | Paket + kendi düzenlemen | $30–80 |

**En kritik kural: silah paketlerini animasyonlarıyla al.** 12 silah × 9 animasyon = 108 animasyon. Solo bunu üretemezsin, üretmeye kalkarsan proje burada durur.

### 5.3 Kendin Üretmen Gerekenler
Satın alınamayan, oyunun kimliğini oluşturan şeyler:
- **Harita düzeni** (level design — asset'ler kit, düzen senin)
- **Wonder weapon** (imza silahı, jenerik olamaz)
- **Kart ikonografisi ve UI kimliği**
- **Aydınlatma ve post-processing geçişi** — asset flip algısını önleyen tek şey bu

### 5.4 Lisans Kontrolü
Satın aldığın her paketin ticari kullanım lisansı olduğunu doğrula. Asset Store standart lisansı ticari kullanıma izin verir ama **redistribution** (asset'in kendisini dağıtma) yasak — bu oyun içinde kullanımı engellemez. Ücretsiz asset'lerde CC lisans şartlarını (atıf gerekliliği) kontrol et, gerekiyorsa krediler ekranına ekle.

---

## 6. Teknik Yığın (Solo Ölçeği)

| Katman | Seçim | Not |
|---|---|---|
| Motor | **Unity 6.3 LTS (6000.3.x)** | LTS, Aralık 2025. Runtime fee 2024'te iptal edildi |
| Lisans | **Unity Personal — ücretsiz** | Yıllık gelir/fon $200k altındaysa bedava. Sen bu sınırın çok altındasın |
| Render | **URP (Forward+)** | Stilize sanat + Deck performansı |
| Netcode | **FishNet (ücretsiz)** | Ücretsiz sürümde prediction + lag compensation var, dokümantasyonu iyi, Discord topluluğu aktif. Solo için NGO'dan daha az sürtünme |
| Taşıma | **Steam Datagram Relay**, `FishyFacepunch` veya `FishySteamworks` | NAT delme yok, IP gizli, ücretsiz |
| Test | **Unity Multiplayer Play Mode** | Editörde 2–4 sanal oyuncu. Solo netcode testi için hayat kurtarıcı — bu olmadan her testte 4 build almak zorunda kalırsın |
| Ses | **Unity built-in audio** (v1) | FMOD'u v1'de kurma; ek karmaşıklık. Gerekirse sonra geç |
| Fizik / AI | Unity Physics + NavMesh | DOTS'a GİRME |
| Sürüm kontrolü | **Git + Git LFS** | LFS olmadan Unity projesi repo'yu şişirir |
| Build | GitHub Actions + GameCI (opsiyonel) | Solo'da manuel build de kabul edilebilir |

### 6.1 Ağ Mimarisi
```
[Oyuncu 1 = HOST]  ←→  Steam Relay  ←→  [Oyuncu 2, 3, 4]
   • Zombi AI, tur mantığı, ekonomi, hasar doğrulama = HOST otoritesi
```

- **Host-otoriteli.** Adanmış sunucu yok, maliyet yok
- Oyuncu hareketi: client-side prediction (FishNet Prediction)
- Hitscan: lag compensation, "favor the shooter", ~200ms geri sarma
- **Zombiler için `NetworkTransform` KULLANMA.** 40 zombi × 20Hz = bant genişliği çöker
  - Özel toplu snapshot: 12–15Hz, quantize pozisyon (16-bit), sadece Y rotasyonu (8-bit), delta sıkıştırma
  - Client tarafında interpolasyon
  - Hedef: **< 80 KB/s host upload**
- Host göçü **yok**. Host çıkarsa oyun biter, skor kaydedilir. UI'da açıkça belirt

### 6.2 Performans Bütçesi
| Metrik | Hedef |
|---|---|
| Eşzamanlı zombi | 40 (32 aktif AI + 8 basit) |
| Kare hızı | 1080p / 60 FPS @ GTX 1060 |
| Steam Deck | 720p / 60 FPS (stilize sanat sayesinde rahat) |
| CPU | 16ms/kare; AI ≤ 4ms, ağ ≤ 2ms |
| Disk | < 6 GB |

**Zorunluluklar:** her şey object pool'dan · yol bulma bütçesi kare başına maks 6–8 agent · 15m üstü zombilerde `Animator.cullingMode = CullCompletely` · GPU instancing + tek atlas · maks 8 eşzamanlı ragdoll · occlusion culling + baked lighting

---

## 7. Claude Code ile Geliştirme

Claude Code bu projede en çok C# sistem kodunda, veri doldurmada ve refactor'da kazandırır. Editör işlerini (sahne kurulumu, prefab, animator, aydınlatma) yapamaz — o kısım sende.

### 7.1 Repo Yapısı ve `CLAUDE.md`
Repo kökünde bir `CLAUDE.md` tut. Buraya yazılacaklar:
- Unity sürümü, netcode kütüphanesi, hedef platform
- Kod konvansiyonları (namespace, isimlendirme, dosya başına tek sınıf)
- **Mimari kuralı:** "Oyun mantığı MonoBehaviour'dan bağımsız plain C# sınıflarında yazılır"
- Yasak listesi ("`GameObject.Find` kullanma", "`Update` içinde allocation yapma", "zombi senkronizasyonunda NetworkTransform kullanma")
- Klasör haritası

### 7.2 En Önemli Mimari Karar: Mantığı MonoBehaviour'dan Ayır

Bu tek karar, Claude Code'un bu projede işe yarayıp yaramayacağını belirler.

```
Assets/Scripts/
  Core/           ← saf C#, Unity'ye bağımlı DEĞİL. Claude burada özgür çalışır ve test edebilir.
    RoundScaling.cs      (tur → zombi sayısı/can/hız)
    Economy.cs           (puan kazanma/harcama kuralları)
    CardPool.cs          (draft mantığı, etiket bonusları)
    DropTable.cs         (akıllı drop hesabı)
    DamageCalculator.cs  (hasar + kart etkileri)
  Runtime/        ← MonoBehaviour + FishNet. Core'u çağırır.
  Data/           ← ScriptableObject tanımları (silah, kart, zombi)
  Tests/          ← Core için unit testler
```

Bu ayrım sayesinde Claude Code, Unity editörünü açmadan `dotnet test` ile 45 kartın etkileşimini doğrulayabilir. Tüm mantığı MonoBehaviour'lara gömersen Claude kör çalışır ve her değişikliği elle test etmen gerekir.

### 7.3 Claude Code'un İyi Olduğu İşler
- Saf C# sistem mantığı (ekonomi, tur ölçekleme, kart efektleri, drop tablosu)
- **45 kartın veri doldurması** — ScriptableObject şemasını sen tasarla, kartları Claude üretsin
- Object pool, state machine, event bus gibi altyapı boilerplate'i
- Unit test yazma
- Refactor ve "şu sistemi şu desene çevir" işleri
- Editor tool'ları (kart dengeleme penceresi, harita istatistik aracı)
- Yerelleştirme dosyası yönetimi

### 7.4 Claude Code'un Zorlandığı / Yapamadığı İşler
- **Netcode hata ayıklama.** Ağ bug'ları zamanlama ve durum senkronizasyonu sorunudur, kodu okuyarak bulunmaz. Multiplayer Play Mode'da sen izleyeceksin
- Sahne kurulumu, prefab hiyerarşisi, animator state machine, aydınlatma bake
- "Hissiyat" ayarı — silah geri tepmesi, kamera sarsıntısı, ses zamanlaması. Bunlar oynayarak ayarlanır
- Level design
- Performans profillemesi (Unity Profiler'ı sen okuyacaksın)

### 7.5 Çalışma Disiplini
- **Küçük commit'ler.** Her sistem kendi branch'inde. Netcode değişikliklerini asla doğrudan main'e atma
- Claude'a tek seferde bir sistem ver. "Tüm oyunu yaz" demek çalışmaz
- Üretilen her kodu Multiplayer Play Mode'da 2 sanal oyuncuyla test et — solo modda çalışan kod ağda çalışmayabilir
- Denge değerlerini koda gömme, ScriptableObject'e koy. Böylece dengelemeyi editörde yaparsın, Claude'a geri dönmen gerekmez

---

## 8. Arkadaşlarla Test Akışı

3 arkadaşın senin en büyük avantajın — çoğu solo geliştiricinin 4 kişilik co-op test imkânı yok.

**Dağıtım seçenekleri:**
1. **Steam Playtest** (önerilen) — Steamworks'te ücretsiz, ayrı app ID gerektirmez, davetiye ile erişim. Gerçek Steam ortamında test = relay, lobi, davet sistemi hepsi gerçek koşulda sınanır
2. Steam beta branch + şifre — erken aşama için
3. itch.io özel link — en hızlısı ama Steam entegrasyonunu test etmez

**Test ritmi:**
- **Haftada 1 seans, 60–90 dakika.** Düzenli olsun, sürprizli değil
- Her seansta **tek bir soruyu** yanıtla ("kart draft'ı akışı bozuyor mu?", "20. turda hâlâ eğlenceli mi?")
- Seans sırasında oynamayı bırakıp not al — sen oynarken bug göremezsin
- **Yüz ifadelerine bak, söylediklerine değil.** "İyiydi" demek veri değil. Nerede sıkıldıkları, nerede güldükleri veri
- Her seans sonrası 10 dakikalık sesli geri bildirim, kaydet

**Ölçülecek metrikler (oyuna telemetri koy):**
- Ortalama ulaşılan tur
- Hangi kartlar seçiliyor / hangileri hep atlanıyor (denge verisi)
- Nerede ölünüyor (harita sıcaklık haritası)
- Hangi silahlar hiç satın alınmıyor
- Oturum süresi ve terk noktası

---

## 9. Zaman Çizelgesi

İki senaryo. Dürüst ol, hangisinde olduğunu bil.

| Aşama | Çıktı / Çıkış kriteri | Part-time (~20s/hafta) | Full-time |
|---|---|---|---|
| **M0 — Teknoloji doğrulama** | FishNet + Steam relay ile 4 oyuncu ve 40 hareketli küp senkron, bant genişliği ölçülmüş | 3 hafta | 1,5 hafta |
| **M1 — Çekirdek prototip** | Gri kutu harita, 1 silah, 1 zombi, tur sistemi, ekonomi, diriltme. **Karar noktası: eğlenceli mi?** | 8 hafta | 4 hafta |
| **M2 — Sistemler tamam** | Kart draft'ı, drop'lar, modifikatörler, FORGE-9, 6 silah, 3 zombi. Hâlâ gri kutu | 12 hafta | 6 hafta |
| **M3 — Sanat geçişi** | Satın alınan asset'ler entegre, aydınlatma, VFX, ses. Harita tam. Trailer çekilebilir | 10 hafta | 5 hafta |
| **M4 — İçerik tamam** | 12 silah, 5 zombi, boss, 45 kart, UI, başarımlar, lokalizasyon | 12 hafta | 6 hafta |
| **M5 — Demo & Next Fest** | Demo build, Steam sayfası aktif, Next Fest. **Hedef: 7.000 istek listesi** | 8 hafta | 5 hafta |
| **M6 — Cila & yayın** | Bug fix, denge, Deck doğrulaması, yayın | 8 hafta | 5 hafta |
| **TOPLAM** | | **~15 ay** | **~8 ay** |

Buna %25 tampon ekle. Part-time gerçekçi hedef: **18 ay.**

**Kritik yol:** M0 → M1. Bu ikisi tıkanırsa proje ölür. M0'daki teknoloji doğrulamasını atlamak solo geliştiricinin yapabileceği en pahalı hata — 6 ay kod yazıp netcode'un ölçeklenmediğini keşfetmek projeyi bitirir.

---

## 10. Bütçe

| Kalem | Düşük | Yüksek |
|---|---|---|
| Unity Personal | $0 | $0 |
| Asset paketleri (§5.2) | $400 | $1.500 |
| Müzik (lisans/freelance) | $150 | $600 |
| Steam Direct | $100 | $100 |
| Claude Code aboneliği (~15 ay) | $300 | $1.500 |
| Trailer (freelance veya kendin) | $0 | $700 |
| Capsule/store sanatı (freelance) | $100 | $400 |
| Hukuki danışma | $200 | $500 |
| Şirket kurulumu / vergi | $150 | $500 |
| Pazarlama (Next Fest ücretsiz, ek reklam) | $0 | $500 |
| **TOPLAM** | **~$1.400** | **~$6.300** |

**Başabaş:** $13 fiyat, Steam %30 payı ve vergi sonrası net ~$8/satış. Yüksek senaryoda ~800 satış. Bu ulaşılabilir bir eşik — asıl hedef zamanının karşılığını almak, ki o da 5.000+ satış demek.

---

## 11. Risk Kaydı

| Risk | Olasılık | Etki | Önlem |
|---|---|---|---|
| **Proje bitmez / motivasyon düşer** | **Çok Yüksek** | **Kritik** | Kapsamı bir daha kesme, M1'de oynanabilir bir şey çıkar, arkadaşlarla düzenli test = dış motivasyon |
| Netcode 40 zombide çöker | Yüksek | Kritik | M0 doğrulaması, özel snapshot, NetworkTransform yasağı |
| Kapsam kayması | Çok Yüksek | Yüksek | §1'deki tablo sözleşme. Yeni özellik = eski bir özellik silinir |
| "Asset flip" algısı | Yüksek | Yüksek | Tutarlı aydınlatma/post-process geçişi, özel wonder weapon ve UI, tek sanat yönü |
| Netcode hata ayıklamada tıkanma | Yüksek | Yüksek | Multiplayer Play Mode, FishNet Discord, ağ kodunu ayrı branch'te tut |
| Steam görünürlüğü alınamaz | Yüksek | Yüksek | Sayfa 4 ay önce açılır, demo + Next Fest, içerik üreticisi anahtarları |
| 45 kartın dengesi bozuk | Orta | Orta | Telemetri, arkadaş testleri, kart verisi ScriptableObject'te (kodsuz ayar) |
| IP ihlali | Düşük | Kritik | §2 kuralları + hukuki danışma |
| Tek geliştirici hastalanır/çıkar | — | — | Git'te her şey, `CLAUDE.md` güncel, dokümantasyon yaz |

---

## 12. Steam Kontrol Listesi

- [ ] Steamworks hesabı + Steam Direct ($100, geri alınabilir)
- [ ] Vergi formu (W-8BEN / W-8BEN-E — ABD stopajı için, şahıs veya şirket)
- [ ] Uygulama oluşturulduktan sonra **30 gün bekleme** zorunlu
- [ ] Mağaza sayfası yayından **en az 3–4 ay önce** canlı
- [ ] Capsule sanatı (tüm boyutlar), 6+ ekran görüntüsü, 1–2 trailer
- [ ] IARC yaş derecelendirmesi
- [ ] Bölgesel fiyatlandırma (Steam'in TR önerisini kullan)
- [ ] Steam Playtest kurulumu (arkadaş testleri için)
- [ ] Steam Cloud, Başarımlar, Liderlik Tabloları, Rich Presence
- [ ] Steam Deck testi + doğrulama başvurusu
- [ ] Demo (ayrı app ID)
- [ ] **Steam Next Fest** — bir kez kullanılabilir, demo hazır olduğunda harca
- [ ] Basın kiti + içerik üreticisi anahtarları
- [ ] İlk 72 saat hotfix planı

---

## 13. Başarı Kriterleri

| Metrik | Hedef |
|---|---|
| Yayın günü istek listesi | ≥ 7.000 |
| İlk ay satış | 3.000–10.000 |
| Steam puanı | ≥ %80 Olumlu |
| Ortalama oturum | ≥ 30 dakika |
| Ortalama tur derinliği | 15–25 |
| Crash oranı | < %1 oturum |

---

## 14. İlk 30 Gün

1. **Hafta 1 — Teknoloji doğrulaması.** Boş sahne, FishNet + Steam relay, 4 oyuncu, 40 hareketli küp. Bant genişliğini ölç. *Bu başarısızsa mimariyi baştan düşün — kod yazmaya başlama.*
2. **Hafta 1 — Repo kurulumu.** Git + LFS, `CLAUDE.md`, §7.2'deki klasör yapısı, Core için test projesi.
3. **Hafta 2 — Steamworks hesabı aç, $100 öde.** 30 günlük saat başlasın, sonra unutursun.
4. **Hafta 2 — Sanat yönü kararı.** Referans panosu topla, alacağın asset paketlerini seç ve stil tutarlılığını doğrula. *Satın almadan önce paketlerin birbirine uyduğundan emin ol.*
5. **Hafta 2–3 — Gri kutu harita.** 4 bölge, kapılar, pencereler. Sadece blokaj, sıfır sanat.
6. **Hafta 3–4 — Çekirdek döngü.** 1 silah + 1 zombi tipi + tur sistemi + ekonomi + diriltme, ağ üzerinde çalışır hâlde.
7. **Hafta 4 — İlk arkadaş testi.** 4 kişi, 45 dakika.

**Tek soru:** *"20 dakika oynadıktan sonra tekrar oynamak istediniz mi?"*

Cevap "hayır" ise sanat, asset ve içerik üretimine geçme. Gri kutuda eğlenceli olmayan bir oyun, güzel grafiklerle de eğlenceli olmaz. Bu türde oyunun kaderi o ilk 20 dakikada belirlenir.

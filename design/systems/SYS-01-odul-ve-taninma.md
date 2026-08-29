# SYS-01 — Ödül ve Tanınma

**Durum:** karara bağlandı (2026-08-29) · **Sahibi:** game-designer
**Sütunlar:** PILLAR-02 (takım muhtaçlığı) · PILLAR-03 (kesintisiz ritim)

---

## Tasarım problemi

Hedef takımca hayatta kalmak. Oyuncular arası skor yarışı **reddedildi** — yarış,
birbirine muhtaçlığı bozar. Ama iyi oynayan tanınmalı. Yani: yarış üretmeden tanıma
üretmek gerekiyor.

## Bağlayıcı kural: ölçtüğün şey oynanan şey olur

Ekrana koyduğun her metrik farmlanır. Çözüm metrikleri gizlemek değil, **istismarı zaten
istenen davranış olan** metrikleri seçmek.

| Metrik | Farmlanabilir mi | Farmlanırsa ne olur | Kullanılır mı |
|---|---|---|---|
| Barikat tamiri | Evet | Barikatlar tamir edilmiş olur | ✅ |
| Diriltme sayısı / ulaşma süresi | Hayır — arkadaşının düşmesini kontrol etmiyorsun | — | ✅ |
| Takım arkadaşının üstünden zombi alma | Hayır | — | ✅ |
| Takım için çekilen hasar | Güvenli şekilde hayır | — | ✅ |
| Sürüyü üstüne çekme | Kısmen | Sürü dağıtılmış olur | ✅ |
| **Kill sayısı** | **Evet** | **Darboğaz kapılır, kill çalınır — takım aleyhine** | ❌ manşet metriği değil |

---

## Dört katman

### 1. Anlık sosyal tanınma — *en güçlü, neredeyse bedava*
Diriltme, kurtarma, barikat gibi eylemler gerçekleştiği **saniyede** herkesin ekranında
kısa bir bildirim + ses. Dopaminin büyük kısmı buradan gelir: dört kişi sesli konuşurken
görülmek, hiçbir run-sonu ekranının veremeyeceği bir şeydir.
*Maliyet: bir UI elemanı + olay hattı.*

### 2. Run sonu sicil — *tanıma, yarış değil*
Manşet takımın: **"14. tura kadar dayandınız."**
Altında her oyuncu davranışına göre bir unvan alır:

| Unvan | Neyi ölçer |
|---|---|
| Duvar | En çok barikat tamiri / takım için çekilen hasar |
| Sağlıkçı | En çok diriltme, en hızlı ulaşma |
| Kurtarıcı | Takım arkadaşının üstünden en çok zombi alma |
| Mıknatıs | Sürüyü en çok üstüne çekme |
| Kasa | Takıma en çok kapı açma / kaynak bırakma |

**Kritik kural:** unvanlar birbirleriyle kıyaslanamaz. Kimse "birinci" değildir, herkes
bir tanesini alır. Sıralama yoktur.

### 3. Meta — biriken para **kart havuzunu** açar
Kritik ayrım: *daha iyi kart çekmek* istismar açığıdır ve zorlanan oyuncuyu tam da yardıma
ihtiyacı olduğu anda geride bırakır (PILLAR-02 ihlali). *Havuza yeni kart eklemek* değildir
— güç değil **çeşitlilik** verir, herkese aynı havuzu açar, manipüle edilemez.

Yirminci run'da ilk kez görülen bir kart, aynı kartı %5 daha iyi çekmekten fazla haz verir.
Bu aynı zamanda sonsuz tur yapısının en büyük zayıflığını kapatır: kaybedince elde bir şey
kalması.

### 4. Kozmetik — yalnızca FPS'te görünen
Silah tılsımı, eldiven, kart draft masasının görünümü. Karakter kostümü **yok**: FPS'te
kendini görmezsin ve kostüm satın alınamaz, karakter rig'ine uymak zorundadır.

### (Onaylandı) Kusursuz tur ödülü
Kimse düşmeden biten turda sonraki draft **3 yerine 4 karttan** olur. Takım hayatta
kalmayı mekanik olarak ödüllendirir. **Denge notu:** iyi giden takımı daha da güçlendirir;
telemetriyle izlenecek, gerekirse 4. kart yalnızca daha düşük nadirlikten gelir.

---

## Mimari sonuçları (M0/M1'de karşımıza çıkar)

- Bu metrikler **sürekli ölçülür** → baştan bir olay/telemetri hattı gerekir. Sonradan
  eklemek her sisteme tek tek dokunmak demektir.
- Kart havuzu açma → run arası kalıcılık + Steam Cloud.
- Tüm sayımlar host otoritesinde tutulur.

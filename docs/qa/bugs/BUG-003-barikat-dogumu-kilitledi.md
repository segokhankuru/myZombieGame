# BUG-003 — Barikat gelince hiçbir zombi doğmadı

**Durum:** düzeltildi · **Bulan:** geliştirici (oyun testi) · **Tarih:** 2026-09-02
**Şiddet:** kritik — oyun oynanamaz · **İş:** M1-08

---

## Belirti

Tur akışı normal görünüyor: `TUR 1: 6 zombi...`, `TUR 2: 8 zombi...` logları düşüyor,
mola sayacı işliyor. **Ama sahada tek bir zombi yok.**

## Kök sebep

İki kavram tek alana bindirildi.

`WindowEntry.IsOpen` başlangıçta "bu pencereden geçilebilir mi" demekti ve doğum noktası
seçimi de onu kullanıyordu: *"açık bir pencere bul, dışında zombi doğur."*

M1-08 barikatı gelince `WindowBarricade`, `IsOpen`'ı barikatın durumuna bağladı. Tam
barikatlı pencere artık "kapalı" oldu — ve **doğum noktası seçimi hiçbir pencereyi uygun
bulamadı.** Turlar döndü, sayaçlar işledi, sahada hiçbir şey olmadı.

## Düzeltme

İki kavram ayrıldı:

| Alan | Anlamı |
|---|---|
| `WindowEntry.IsOpen` | Bu pencere bir **giriş noktası mı** — yapısal, kalıcı |
| `WindowBarricade.AllowsEntry` | Şu an **geçilebilir mi** — anlık, barikata bağlı |

Zombi barikatlı pencerede **doğar** ve onu söker; geçilemiyor olması oranın giriş noktası
olmadığı anlamına gelmez. `WindowBarricade` artık `SetOpen` çağırmıyor.

Ayrıca doğum noktası bulunamadığında **bir kez, yüksek sesle** hata basılıyor. BUG-001'in
dersi burada tekrar işe yaradı: tur akışının dönüp sahanın boş kalması, sessiz kaldığı
sürece teşhis edilmesi en zor hata türü.

## İkinci bulgu: taşınan betikler prefab'ı bozdu

Aynı oturumda Console şunu da veriyordu:

```
The referenced script on this Behaviour (Game Object 'Player') is missing!
```

Sebep ayrı: config üretimi için bazı `.cs` dosyaları geçici olarak proje dışına
alınmıştı — ama **`.meta` dosyaları geride bırakıldı.** Unity, dönen dosyalara yeni GUID
üretti ve o betiklere bakan her prefab bozuldu.

İki önlem:

1. Kurulum aracı artık oyuncu prefab'ındaki bozuk bileşenleri **özyinelemeli** temizliyor
   (tek nesneye bakmak yetmiyor; alt nesnelerdekiler geride kalıyordu) ve temizlenemeyen
   bir kalıntı varsa hangi nesnede olduğunu söylüyor.
2. Kural yazıldı: bir betiği taşımak gerekiyorsa **`.cs` ve `.cs.meta` birlikte taşınır.**
   Tek başına `.cs` taşımak, o betiğe bakan bütün sahne ve prefab referanslarını sessizce
   koparır.

## Alınan ders

**Bir alanın anlamını genişletmek, onu kullanan her yeri gözden geçirmeyi gerektirir.**
`IsOpen` iki farklı soruya cevap veriyordu ve ikisi ayrıştığı an biri yanlış cevap
vermeye başladı — üstelik hiçbir hata mesajı olmadan.

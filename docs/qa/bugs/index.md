# Hata dizini

Hepsi **kapalı**. Ortak ders: *sessiz başarısızlık en pahalı hata türüdür* — beşinin
dördü hiçbir mesaj vermiyordu ve teşhis oyun testine kaldı.

| Hata | Konu | Ders |
|---|---|---|
| [BUG-001](BUG-001-sunucu-sarjoru-hic-dolmuyor.md) | Sunucunun gölge şarjörü hiç dolmuyordu | Kaynağı sayan sistem, kaynağı geri veren yolu aynı anda yazmalı |
| [BUG-002](BUG-002-mesru-atislar-reddediliyor.md) | Meşru atışlar reddediliyordu | Sunucu doğrulaması eşitlik değil **makuliyet** testidir |
| [BUG-003](BUG-003-barikat-dogumu-kilitledi.md) | Barikat doğumu kilitledi | "Geçilemiyor" ile "giriş noktası değil" ayrı sorular (`IsOpen` / `AllowsEntry`) |
| [BUG-004](BUG-004-kapi-gorunmuyor-zombiler-icerde.md) | Kapı görünmüyor, zombiler içeride | Aracın **yanlış anda** ölçmesi, yanlış ölçmesinden tehlikelidir |
| [BUG-005](BUG-005-havuzdan-cikan-zombi-eski-yerinde-beliriyor.md) | Havuzdan çıkan zombi eski yerinde beliriyor | Bir bileşen bir alanın sahibiyse o alana başka kimse yazmamalı |

Yeni hata: `/bug`.

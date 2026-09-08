# ADR dizini

Her satır bir mimari karar. Önce burası okunur, sonra gereken dosyaya inilir.

| ADR | Karar | Durum |
|---|---|---|
| [0001](ADR-0001-netcode-kutuphanesi.md) | Netcode: FishNet + Pro | **supersede edildi** → 0004 |
| [0002](ADR-0002-unity-surumu.md) | Unity 6000.3 LTS (6.5 reddedildi: LTS değil) | kabul |
| [0003](ADR-0003-render-pipeline.md) | URP (Forward+); HDRP bakım modunda | kabul |
| [0004](ADR-0004-netcode-kutuphanesi-mirror.md) | Netcode: Mirror (MIT), host otoriteli, hareket client-authoritative | kabul |
| [0005](ADR-0005-config-importer-ve-bunker-config.md) | Config importer + `Bunker.Config`: denge sayıları yalnızca `config/` içinde | kabul |
| [0006](ADR-0006-ses-assembly-si-ve-uretilen-sfx.md) | `Bunker.Audio` assembly'si; sesler çalışma anında sentezleniyor, varlık dosyası yok | kabul |
| [0007](ADR-0007-steam-daveti-ve-tasima.md) | Steam daveti ve tasima: FizzyFacepunch, KCP yolu korunur; ucuncu parti kaynakta iki yama | kabul - iki makinede test bekliyor |
| [0008](ADR-0008-magaza-sanati-ve-urp-kopyalama.md) | Asset Store sanati: ucuncu parti klasoru salt okunur, URP kopyasi uretilir; yerlesim olculur, `art.asset`'te saklanir | kabul |

Yeni ADR: `/adr` — numarayı buradan devam ettirir ve bu tabloya bir satır ekler.

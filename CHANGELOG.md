# Changelog

Newest first. Append-only - rewriting loses the record.

## 2026-09-09 (2) — Silah tasima siniri, durbun gorunumu ve co-op

**Duzeltilen**
- **Silahlar artik dogru yone bakiyor.** Low Poly Weapons VOL.1 paketi namluyu -Z'ye
  ciziyormus; tablo +Z yaziyordu ve silahlar oyuncuya donuktu.
- **Bicak ve atesli silah artik ayni anda gorunmuyor** - hangisi eldeyse yalnizca o.
- **Tezgahtan E ile cikilabiliyor.** E ayni karede hem kapatip hem aciyordu.
- **M107'nin durbunu duzeldi**: kisa menzil scope'u takiliydi, artik uzun menzil (LR).

**Kontroller**
- Slot duzeni: **1 bicak, 2-3 atesli silah, 4-8 esya** (eskiden 2-5 ve 6-0).
- **F** tur arasinda HAZIR. Ayakta olan herkes hazir verirse mola aninda biter.

**Silahlar**
- **Ayni anda en fazla 2 atesli silah tasinir.** Tezgahtan alinan silah **eldekinin**
  yerine gecer.
- **Satin alinmis silahlar run boyunca hatirlanir** ve tezgahtan **bedelsiz** geri
  alinabilir - mermisiyle birlikte, biraktigin gibi.
- **M4'e ELCAN durbunu** eklendi (sag tik 3x).

**Durbun gorunumu** - artik gercekten durbunden bakiyorsun
- **M107 (6x):** ekran karariyor, ortada yuvarlak alan, ince arti + mil noktalari.
  Cevreni goremezsin - uzun menzilin bedeli.
- **M4 (3x):** prizmali optik. Govde gorunur kalir, ekran kararmaz, kirmizi chevron
  nisangah. Kalabalikta da kullanilabilir.

**Denge**
- Sarjor kartlari **%10-20-40** (eskiden 20-40-70). Tezgah hatti zaten alis basina
  taban degerin %10'uydu - degismedi, ikisi artik ayni dili konusuyor.
- Tur arasi **sabit 30 sn**.

**Co-op**
- **Yere dusen 10 saniyede kanayarak olur** (eskiden tur sonuna kadar beklerdi).
  Ekranda geri sayim var.
- **Tur basinda dirilis puan istiyor** (500 + tur x 100). Puanin yetmezse yettigi kadar
  **canla** dirilirsin; hic yoksa 1 can.
- **Olurken esyalarin gider ve yedek mermin yarilanir.** Sarjordeki mermiye
  dokunulmaz.
- Spectate zaten vardi: yerdeyken/oluyken arkadasini izlersin, bosluk tusu izlenen
  kisiyi degistirir.

## 2026-09-09 — Envanter, silahlar, ses ve karakter

**Kontroller degisti**
- **Bicak artik 1 numarali slotta** (V tusu KALKTI). 2-5 atesli silahlar, 6-0 esyalar.
  Fare tekerlegi bicak dahil butun slotlari dolasir.
- Sag tik **M107'nin durbunu** (basili tut, 4x). Diger silahlarda bir sey yapmaz.

**Esyalar**
- Yerden toplanan droplar artik **cepte birikiyor** (slot basina 5) ve istedigin an
  kullaniliyor. 6 can, 7 mermi, 8 yavaslatma, 9 dondurma, 0 nuke.
- Slot doluyken esya **yerde kalir** - sessizce yok olmaz.
- **Nuke sahayi silmiyor:** en yakin 15 zombiyi oldurur.
- Dondurma 5 -> 10 sn, yavaslatma 10 -> 20 sn.

**Can**
- Tur icinde **kendiliginden dolmuyor**. Tur bitince **tam dolum**.
- `Cabuk Toparlanma` karti kaldirildi (dayandigi sistem kalkti).

**Silahlar** — hepsi Low Poly Weapons VOL.1'den, tezgah ve duvar teshirleri dahil
| Silah | Hasar | Sarjor | Not |
|---|---|---|---|
| M1911 (tabanca) | 55 | 8 | dolum 1.6 -> 1.15 sn |
| Uzi (taramali) | 55 | 30 | tabancayla ayni hasar |
| Benelli M4 (pompali) | 24 x8 sacma | 6 | hasar degismedi |
| AK-74 | 77 | 20 | taramalidan %40 fazla |
| M4 | 66 | 30 | taramalidan %20 fazla |
| M107 (sniper) | 132 | 5 | M4'un iki kati, durbunlu |

- **Tur sonu mermi ikmali artik butun silahlara gidiyor**, yalnizca eldekine degil.

**Ses** (ADR-0009)
- Ana menude muzik + ayarlarda **muzik seviyesi** kaydiraci (varsayilan %50).
  Oyuna girince muzik susar.
- Oyuncunun **ayak sesi** var (yurume/kosu/inis, mesafeye gore).
- Silahlarin **gercek atis ve dolum sesleri** var.

**Karakter**
- Oyuncunun **gercek bir govdesi** var (Human Basic Motions) ve yuruyus/kosu
  animasyonlari isliyor. Kendi govden yalnizca **golge** cizer.

**Arayuz**
- Ekranin solunda **tur tehdit paneli**: zombinin hasari, hizi ve hiz kademesi.
  Boss turunda uyari rengine doner. (Sayi zombi can barlarinin yanindan kalkti.)
- Ekranin altinda **slot cubugu**: elindeki silah, sahip oldugun silahlar ve esya
  yiginlari.

**Duzeltilen**
- **Maratoncu karti artik havuzda gorunuyor.** Kart `cards.json`'a eklenmisti ama
  oyunun okudugu katalog varligi yeniden uretilmemisti; kart hic cikmiyordu. Ayni
  nedenle Sarapnel de eski gucuyle (0.35) calisiyordu - artik 0.60.

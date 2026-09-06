# ADR-0006 — Ses için ayrı bir assembly ve üretilen SFX

- **Durum:** kabul edildi
- **Tarih:** 2026-09-05
- **Karar veren:** technical-director (geliştirici onayıyla)
- **İlgili:** ADR-0004 (Mirror), `.claude/rules/audio-code.md`, `docs/architecture/ARCHITECTURE.md`

## Bağlam

İlk oyun testinin bulgusu tek cümleydi: **"arkamdan gelen zombiyi hiç duymayınca
heyecanını, korkusunu test edemiyorum."** Bu bir cila eksiği değil, bir oynanış eksiği:
oyuncunun arkasını dönmesi için bir sebep yoksa harita bilgisi tek yönlü kalır ve
PILLAR-04 (kaosta okunabilirlik) yalnızca ekranın gördüğü 90 derecede geçerli olur.

İki kısıt vardı:

1. **`Bunker.Systems` motor referansı taşımıyor** (`noEngineReferences: true`). Ses
   `AudioSource` demektir, yani oraya giremez. Ama ses üreten taraflar dört ayrı
   assembly'de: `Gameplay` (silah), `AI` (zombi), `UI` (menü), `Net`.
2. **Sanat/ses yönü henüz kilitlenmedi.** `/art-direction` ve `/audio-direction`
   çalışmadı. Kilitlenmeden alınan her ses dosyası sonra atılacak iş, ve lisans kaydı
   gerektiren bir borç (`audio-code.md`).

## Seçenekler

| # | Seçenek | Neden değil |
|---|---|---|
| 1 | Sesi `Gameplay`'e koy | `AI` ve `UI` oraya bakamaz; `Gameplay`'e bağımlılık ters yön |
| 2 | Her sistem kendi `AudioSource`'unu sürsün | `audio-code.md`'nin birinci kuralının ihlali; ses limiti ve öncelik imkânsızlaşır |
| 3 | `Bunker.Systems`'ten motor kısıtını kaldır | Test edilebilirliğin temeli o kısıt; sahne açmadan koşan EditMode testleri onun sayesinde var |
| 4 | **Ayrı `Bunker.Audio` assembly'si** | — |
| 5 | Ses dosyası satın al / indir | Yön kilitlenmeden yapılan seçim; lisans kaydı ve atılacak iş |

## Karar

**`Bunker.Audio` adında, yalnızca `Bunker.Systems`'e bakan yeni bir assembly.**
`Gameplay`, `AI`, `UI` ve `Editor` ona referans verir; **hiç kimse ondan bir şey
beklemez** — yani bağımlılık grafiği tek yönlü kalır ve `Systems` hâlâ zeminde durur.

Dışarıya açık olan tek şey iki metottur:

```csharp
GameAudio.Play(SfxId.GunShot);                 // 2B - oyuncunun kendi eylemi
GameAudio.PlayAt(SfxId.ZombieGroan, position); // 3B - yönü ve mesafesi duyulur
```

**Sesler çalışma anında sentezlenir** (`SfxBank`): tek bir `.wav` dosyası yok. Her ses
sabit tohumlu dört varyant üretir, tembel olarak ilk çalınışta. Bu, gri kutunun sesli
karşılığıdır — *okunabilir olsun, güzel olmasın.*

## Sonuçları

**İyi:**
- Ses yönü kilitlendiğinde değişecek tek dosya `SfxBank`'tır; çağıran kodun tek satırı
  değişmez, çünkü çağıran kod bir dosya adı değil bir **oyun olayı** söyler.
- Ses limiti, öncelik ve varyasyon tek yerde: kırk eşzamanlı vuruş kırpma değil seyrelme
  üretir.
- Depoya tek bayt ses varlığı girmez; lisans borcu yok.

**Kötü:**
- Sentezlenen sesler yayın kalitesinde değil ve olmayacak. Bu geçicidir ve öyle
  işaretlenmiştir.
- Yeni bir assembly, derleme grafiğinde yeni bir düğüm.
- İlk çalınışta örnek üretimi bir kerelik bir maliyet (ses başına < 2 ms, ölçülmedi —
  ölçüm `/perf-check`'in işi).

**Yapılmadı, bilerek:** bus haritası, ducking, karışım (mix), müzik. Hepsi
`/audio-direction`'ın işi; burada yalnızca tek bir ana seviye var
(`GameAudio.MasterVolume`).

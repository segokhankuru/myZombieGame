# Bir denge sayısını nasıl değiştirirsin

**Kısa cevap:** JSON'u düzenle, importer'ı çalıştır. C# dosyalarına ve Inspector'a
dokunma — ikisi de üretilmiş çıktıdır.

---

## Değeri değiştirmek

1. `config/balance/<alan>.json` içindeki sayıyı değiştir
   (`rounds.json`, `economy.json`, `zombie.json`).
2. Doğrula:

```bash
powershell -NoProfile -ExecutionPolicy Bypass -File .claude/tools/config-validate.ps1
```

3. Varlıkları güncelle — Unity kapalıyken:

```bash
powershell -NoProfile -ExecutionPolicy Bypass -File .claude/tools/unity-exec.ps1 -Method Bunker.Editor.ConfigTools.ConfigImporter.GenerateCodeBatch
```

```bash
powershell -NoProfile -ExecutionPolicy Bypass -File .claude/tools/unity-exec.ps1 -Method Bunker.Editor.ConfigTools.ConfigImporter.FillAssetsBatch
```

Unity açıksa menüden tek adım: **Bunker → Config → Ice Aktar (kod + varlik)**.
Derleme bitince varlıklar kendiliğinden dolar.

> **Neden iki komut:** birincisi C# üretir, Unity onu derler, ikincisi varlıkları
> doldurur. Yeni üretilmiş bir tip, onu üreten derleme turunun içinde henüz yoktur.

---

## Yeni bir ayar eklemek

Sırayı bozma, çünkü şema sözleşmedir:

1. `config/schema/<alan>.schema.json` içine anahtarı ekle. **Üçü de zorunlu:**
   `type`, `minimum`/`maximum`, ve `description`.
2. `description` şunu söylemeli: *aralığın dışına çıkarsan oyuncu ne hisseder.*
   "Zıplama yüksekliği" bir açıklama değildir. "Düşürürsen platformlar arası boşluk
   aşılamaz ve seviye kilitlenir; yükseltirsen oyuncu tasarlanan yolu atlar" bir
   açıklamadır. Bu cümle, altı ay sonra birinin eğlenceyi ayarlayıp götürmesini
   engelleyen tek şeydir.
3. Anahtarı `required` listesine ekle.
4. `config/balance/<alan>.json` içine değeri yaz.
5. İçe aktar.

**Bir tunable'ı asla `[SerializeField] float` olarak eklemezsin.** O alan denge
katmanının dışında kalır, JSON'da görünmez, `/tune` ile değişmez ve bir gün başka bir
sayıyla çelişir.

---

## Yeni bir alan (domain) açmak

`config/schema/<yeni>.schema.json` + `config/balance/<yeni>.json` yaz, içe aktar.
Importer geri kalanını üretir: `Bunker.Systems.Config.<Yeni>Config` (saf C#) ve
`Bunker.Config.<Yeni>ConfigAsset` (ScriptableObject).

Üretilen ad kuralı: **grup adı + anahtar adı**, PascalCase.
`count.maxConcurrent` → `CountMaxConcurrent`.

---

## Dokunulmayacaklar

| Dosya | Neden |
|---|---|
| `Assets/_Project/Code/**/Generated/*.g.cs` | Üretilir; bir sonraki içe aktarma üzerine yazar |
| `Assets/_Project/Config/*.asset` | Üretilir; Inspector'dan değiştirirsen JSON ile oyun birbirine yalan söyler |

Bunlardan birini elle değiştirdiğini fark edersen, değişikliği JSON'a taşı ve içe
aktarmayı tekrar çalıştır.

---

## Şeklini değiştirmek (anahtar adı değişikliği / silme)

Üçü birden, aynı değişiklikte (`config-data.md`):

1. Şemada `version` artar
2. `docs/DECISIONS.md`'ye bir satır
3. Anahtar kayıtlıysa (save) bir migration

---

## Hata mesajları ne demek

| Mesaj | Anlamı |
|---|---|
| `'x.y' anahtari semada yok` | Denge dosyasında şemada olmayan bir anahtar var — genelde yazım hatası. Sessizce yok sayılsaydı değişikliğin hiç uygulanmazdı |
| `zorunlu anahtar 'x.y' eksik` | Şema istiyor, denge dosyası vermiyor. Boot'ta sessiz varsayılan yok |
| `'x.y' = 5, en fazla 3 olmali` | Aralık dışı. **Aralığı değere göre genişletme** — ya değer yanlış ya tasarım değişti; hangisi olduğunu söyle |
| `... tipi bulunamadi` | Kod üretimi koşmamış. Önce birinci geçiş |
